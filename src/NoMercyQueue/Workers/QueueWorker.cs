using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Lifecycle;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using BootStage = NoMercy.NmSystem.Lifecycle.BootStage;
using Exception = System.Exception;

namespace NoMercyQueue.Workers;

public class QueueWorker(
    JobQueue queue,
    string name = "default",
    QueueRunner? runner = null,
    ILogger<QueueWorker>? logger = null,
    IServiceScopeFactory? scopeFactory = null,
    IServerReadinessGate? readinessGate = null,
    IServerPhaseTracker? phaseTracker = null
)
{
    private const int MaxTransientRetries = 5;
    private const int TransientRetryBaseMs = 3000;
    private const int TransientRetryJitterMs = 2000;

    private long? _currentJobId;
    private bool _isRunning = true;
    private CancellationTokenSource _stopCts = new();

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
        // Wait for every boot stage so jobs never run alongside in-flight startup
        // work — most notably the FFmpeg download/extract, which would otherwise
        // race the encoder worker's Process.Start and crash mid-encode.
        if (phaseTracker is not null)
        {
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] awaiting boot stages [{BootStage.All}]",
                Serilog.Events.LogEventLevel.Information
            );
            await phaseTracker.WhenReachedAsync(BootStage.All, stopToken).ConfigureAwait(false);
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] all boot stages complete, entering poll loop",
                Serilog.Events.LogEventLevel.Information
            );
        }
        else if (readinessGate is not null)
        {
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] awaiting readiness gate",
                Serilog.Events.LogEventLevel.Information
            );
            await readinessGate.WaitForReadyAsync(stopToken).ConfigureAwait(false);
            NoMercy.NmSystem.SystemCalls.Logger.App(
                $"[QueueWorker {name}] gate resolved, entering poll loop",
                Serilog.Events.LogEventLevel.Information
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
                        string typeName = jobWithArguments?.GetType().FullName ?? "null";
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
                    // Stop() was called — exit the loop cleanly.
                    break;
                }
            }
        }
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

        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    // GetAwaiter().GetResult() rather than .Wait() so that the
                    // original exception propagates unwrapped (not wrapped in
                    // AggregateException) — this keeps catch-block handling and
                    // retry classification correct.
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
        // Walk the exception chain looking for SQLite lock contention:
        //   Error 5  (SQLITE_BUSY)   → "database is locked"
        //   Error 6  (SQLITE_LOCKED) → "database table is locked" (shared-cache contention)
        // We check the type name instead of casting because this assembly
        // does not reference Microsoft.Data.Sqlite directly.
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
        // Replace the cancel source if it was already tripped by a prior Stop;
        // otherwise StartAsync exits immediately on the first cancellation check.
        if (_stopCts.IsCancellationRequested)
            _stopCts = new();
        _isRunning = true;
        _ = Task.Run(() => StartAsync(_stopCts.Token));
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
