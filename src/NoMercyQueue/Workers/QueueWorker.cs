// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Lifecycle;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Core.Resources;
using BootStage = NoMercy.NmSystem.Lifecycle.BootStage;
using Exception = System.Exception;

namespace NoMercyQueue.Workers;

/// <summary>
/// Names of queues whose jobs participate in resource-budget gating.
/// Workers on these queues check <see cref="IResourceBudget"/> before
/// running a job and re-queue it when the budget is saturated.
/// </summary>
internal static class ResourceAwareQueues
{
    internal static bool IsResourceAware(string queueName) =>
        queueName is "encoder-gpu" or "encoder-cpu";
}

public class QueueWorker(
    JobQueue queue,
    string name = "default",
    QueueRunner? runner = null,
    ILogger<QueueWorker>? logger = null,
    IServiceScopeFactory? scopeFactory = null,
    IServerReadinessGate? readinessGate = null,
    IServerPhaseTracker? phaseTracker = null,
    IResourceBudget? resourceBudget = null
)
{
    private static readonly TimeSpan BudgetRetryDelay = TimeSpan.FromSeconds(5);

    private const int MaxTransientRetries = 5;
    private const int TransientRetryBaseMs = 3000;
    private const int TransientRetryJitterMs = 2000;

    private long? _currentJobId;
    private bool _isRunning = true;
    private CancellationTokenSource _stopCts = new();

    /// <summary>
    /// Set when the resource budget gate rejected the most recent acquire
    /// attempt and cleared on the next successful acquire. Used to log the
    /// "budget saturated, retrying" line ONCE per saturation episode instead
    /// of every <see cref="BudgetRetryDelay"/> tick — multiple workers
    /// polling against a fully-leased semaphore otherwise spam the log with
    /// retry notices for the entire duration of the holding ffmpeg.
    /// </summary>
    private bool _suppressBudgetSaturationLog;
    private int _saturationRetryCount;
    private const int SaturationLogInterval = 120; // Log every 10 minutes (120 * 5s)

    private int CurrentIndex => runner?.GetWorkerIndex(name, this) ?? -1;

    /// <summary>
    /// True while this worker holds a reserved job (between
    /// <c>queue.ReserveJob</c> and <c>queue.DeleteJob</c>). False when
    /// the worker thread is alive but waiting for the next job — a
    /// spawned worker is not the same as a busy worker.
    ///
    /// <para>Consumed by the encoder's hardware-benchmark deferral
    /// (see <c>EncoderActivityProbe</c>) so calibration runs only when
    /// the queue is genuinely idle, not just whenever a worker thread
    /// happens to exist.</para>
    /// </summary>
    public bool IsProcessingJob => _currentJobId is not null;

    public event WorkCompletedEventHandler WorkCompleted = delegate { };

    public async Task StartAsync(CancellationToken stopToken)
    {
        if (phaseTracker is not null)
        {
            await phaseTracker.WhenReachedAsync(BootStage.All, stopToken).ConfigureAwait(false);
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] all boot stages complete, entering poll loop"
            );
        }
        else if (readinessGate is not null)
        {
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] awaiting readiness gate"
            );
            await readinessGate.WaitForReadyAsync(stopToken).ConfigureAwait(false);
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] gate resolved, entering poll loop"
            );
        }

        if (stopToken.IsCancellationRequested)
            return;

        bool firstPoll = true;
        while (_isRunning && !stopToken.IsCancellationRequested)
        {
            QueueJobModel? job = queue.ReserveJob(name, _currentJobId);

            if (firstPoll)
            {
                NoMercy.NmSystem.SystemCalls.Logger.App(
                    $"[QueueWorker {name}] first ReserveJob → {(job is null ? "null" : "id=" + job.Id)}",
                    Serilog.Events.LogEventLevel.Information
                );
                firstPoll = false;
            }

            if (job != null)
            {
                // Resource-budget gate: for encoder-gpu / encoder-cpu queues,
                // check whether the budget has a free slot before executing.
                // If not, release the reservation and let the job be picked up
                // again after a short delay.
                ResourceLease? lease = null;

                if (resourceBudget is not null && ResourceAwareQueues.IsResourceAware(name))
                {
                    if (!TryAcquireBudget(job, out lease))
                    {
                        queue.ReleaseReservation(job, BudgetRetryDelay);

                        // Honor the full retry interval — using WorkAvailable
                        // here would let an unrelated Enqueue wake us up early
                        // and immediately re-probe the budget, which spins
                        // through deferred jobs at DB-query rate when many
                        // are stacked up under headroom denial. WaitHandle on
                        // the stop token keeps the sleep cancellation-aware.
                        if (stopToken.WaitHandle.WaitOne(BudgetRetryDelay))
                            break;

                        continue;
                    }
                }

                _currentJobId = job.Id;

                try
                {
                    object jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);

                    if (jobWithArguments is IShouldQueue classInstance)
                    {
                        ExecuteWithTransientRetry(classInstance, job);

                        queue.DeleteJob(job);
                        _currentJobId = null;
                        OnWorkCompleted(EventArgs.Empty);

                        logger?.LogTrace(
                            "QueueWorker {Name} - {CurrentIndex}: Job {JobId} of Type {ClassInstance} processed successfully",
                            name,
                            CurrentIndex,
                            job.Id,
                            classInstance
                        );
                    }
                    else
                    {
                        string typeName = jobWithArguments.GetType().FullName ?? "null";
                        logger?.LogError(
                            "QueueWorker {Name} - {CurrentIndex}: Job {JobId} deserialized to {TypeName} which does not implement IShouldQueue — rejecting",
                            name,
                            CurrentIndex,
                            job.Id,
                            typeName
                        );

                        queue.FailJob(
                            job,
                            new InvalidOperationException(
                                $"Job payload deserialized to {typeName} which does not implement IShouldQueue"
                            )
                        );
                        _currentJobId = null;
                    }
                }
                catch (Exception ex)
                {
                    queue.FailJob(job, ex);

                    _currentJobId = null;

                    logger?.LogError(
                        "QueueWorker {Name} - {CurrentIndex}: Job {JobId} of Type {Payload} failed with error: {Error}",
                        name,
                        CurrentIndex,
                        job.Id,
                        job.Payload,
                        ex
                    );
                }
                finally
                {
                    if (lease is not null)
                        resourceBudget?.Release(lease);
                }
            }
            else
            {
                OnWorkCompleted(EventArgs.Empty);

                try
                {
                    queue.WorkAvailable.Wait(TimeSpan.FromSeconds(5), stopToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Attempts to acquire budget for the given job payload.
    /// Returns true and sets <paramref name="lease"/> when a slot is available.
    /// Returns false when the budget is saturated — caller should release the reservation.
    /// </summary>
    private bool TryAcquireBudget(QueueJobModel job, out ResourceLease? lease)
    {
        lease = null;

        if (resourceBudget is null)
            return true;

        ResourceRequirement? requirement = ExtractRequirement(job);
        if (requirement is null)
            return true;

        try
        {
            lease = resourceBudget.TryAcquire(requirement, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            // The resource budget might throw if a GPU device key is unknown or other logic errors.
            // We treat this as a temporary "saturated" state so the job stays
            // in the queue until hardware detection completes or the requirement
            // changes.
            if (!_suppressBudgetSaturationLog)
            {
                logger?.LogWarning(
                    "[{Queue}] budget acquisition failed for job {JobId}: {Message}. Will retry every {Delay}s.",
                    name,
                    job.Id,
                    ex.Message,
                    BudgetRetryDelay.TotalSeconds
                );
                _suppressBudgetSaturationLog = true;
            }

            return false;
        }

        if (lease is null)
        {
            // Log the FIRST saturated retry per episode, and then every
            // SaturationLogInterval retries thereafter. A long-running
            // ffmpeg bundle can hold the semaphore for tens of minutes; with
            // four workers polling every BudgetRetryDelay against a fully-
            // leased budget, an Info-level log per retry produces thousands
            // of identical lines that drown out the rest of the encoder log
            // and (via synchronous Serilog sinks) contribute to host
            // unresponsiveness. The flag resets on the next successful
            // acquire so a new saturation episode logs once again.
            _saturationRetryCount++;
            bool shouldLog =
                !_suppressBudgetSaturationLog
                || (_saturationRetryCount % SaturationLogInterval == 0);

            if (shouldLog)
            {
                int gpuAvailable = requirement.GpuDeviceKey is not null
                    ? resourceBudget.AvailableGpuEncoderSlots(requirement.GpuDeviceKey)
                    : -1;
                int cpuAvailable = resourceBudget.AvailableCpuThreads();

                logger?.LogInformation(
                    "[{Queue}] budget saturated for job {JobId} ({Requirement}) — GPU slots available: {Gpu}, CPU threads available: {Cpu}. Still retrying every {Delay}s.",
                    name,
                    job.Id,
                    requirement,
                    gpuAvailable,
                    cpuAvailable,
                    BudgetRetryDelay.TotalSeconds
                );
                _suppressBudgetSaturationLog = true;
            }

            return false;
        }

        _suppressBudgetSaturationLog = false;
        _saturationRetryCount = 0;
        return true;
    }

    /// <summary>
    /// Lightly deserializes the job payload to read just the
    /// <see cref="ResourceRequirement"/> from an <see cref="IHasResourceRequirement"/>
    /// job without executing the full deserialization path.
    /// Returns null when the job does not carry resource metadata.
    /// </summary>
    private static ResourceRequirement? ExtractRequirement(QueueJobModel job)
    {
        try
        {
            object deserialized = SerializationHelper.Deserialize<object>(job.Payload);
            if (deserialized is IHasResourceRequirement carrier)
                return carrier.ResourceRequirement;
        }
        catch
        {
            // Deserialization failure here is non-fatal — skip the gate.
        }

        return null;
    }

    protected virtual void OnWorkCompleted(EventArgs e)
    {
        WorkCompleted.Invoke(this, e);
    }

    /// <summary>
    /// Executes a job with transparent retry for transient SQLite errors (SQLITE_BUSY /
    /// "database is locked").  These retries do NOT consume the job's attempt count —
    /// they exist to absorb short-lived write-lock contention that is normal under
    /// concurrent queue workers sharing a single SQLite database.
    /// <para>
    /// When a <see cref="IServiceScopeFactory"/> is available and the job implements
    /// <see cref="IJobStorageInjector"/>, a fresh DI scope is opened per execution,
    /// storage services are resolved and set on the job, and the scope is disposed
    /// when the job completes (success or failure).
    /// </para>
    /// </summary>
    private void ExecuteWithTransientRetry(IShouldQueue job, QueueJobModel queueJob)
    {
        IServiceScope? scope = null;

        if (scopeFactory is not null && job is IJobStorageInjector injector)
        {
            scope = scopeFactory.CreateScope();
            injector.InjectStorageServices(scope.ServiceProvider);
        }

        if (job is IJobIdReceiver idReceiver)
            idReceiver.ReceiveJobId((int)queueJob.Id);

        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    job.Handle().GetAwaiter().GetResult();
                    return;
                }
                catch (Exception ex)
                    when (IsTransientSqliteError(ex) && attempt < MaxTransientRetries)
                {
                    int delay = TransientRetryBaseMs + Random.Shared.Next(TransientRetryJitterMs);

                    logger?.LogWarning(
                        "QueueWorker {Name} - {CurrentIndex}: Job {JobId} hit transient SQLite error (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        name,
                        CurrentIndex,
                        queueJob.Id,
                        attempt + 1,
                        MaxTransientRetries,
                        delay
                    );

                    Thread.Sleep(delay);
                }
            }
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static bool IsTransientSqliteError(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            string typeName = current.GetType().Name;

            if (
                typeName is "SqliteException"
                && current.Message.Contains("is locked", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    public void Stop()
    {
        logger?.LogInformation("QueueWorker {Name} - {CurrentIndex}: stopped", name, CurrentIndex);
        _isRunning = false;
        _stopCts.Cancel();
    }

    public void StopWhenReady()
    {
        while (_currentJobId != null)
            Thread.Sleep(1000);

        Stop();
    }

    /// <summary>
    /// Fire-and-forget wrapper that schedules <see cref="StartAsync"/> on the
    /// thread pool. <see cref="QueueRunner"/> uses this from its synchronous
    /// spawn/lifecycle methods so it doesn't have to await each worker
    /// individually — workers run for the lifetime of the process.
    /// </summary>
    public void Start()
    {
        if (_stopCts.IsCancellationRequested)
            _stopCts = new();
        _isRunning = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await StartAsync(_stopCts.Token);
            }
            catch (Exception ex)
            {
                logger?.LogCritical(
                    ex,
                    "QueueWorker {Name} - {CurrentIndex}: StartAsync crashed",
                    name,
                    CurrentIndex
                );
            }
        });
    }

    /// <summary>
    /// Cancels the current Stop signal and re-enters the poll loop. Used by
    /// <see cref="QueueRunner.Restart"/> so a paused worker can resume without
    /// reconstructing the instance (preserving its DI scope and event handlers).
    /// </summary>
    public void Restart()
    {
        Stop();
        Start();
    }
}
