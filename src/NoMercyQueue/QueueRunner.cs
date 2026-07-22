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

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Core.Resources;
using NoMercyQueue.Workers;

namespace NoMercyQueue;

public class QueueRunner
{
    private sealed class WorkerEntry
    {
        public int Count { get; set; }
        public List<QueueWorker> WorkerInstances { get; } = [];
        public CancellationTokenSource Cts { get; set; } = new();
        public bool IsUpdating { get; set; }

        public WorkerEntry(int count)
        {
            Count = count;
        }
    }

    private readonly object _workersLock = new();

    private readonly Dictionary<string, WorkerEntry> _workers;

    private volatile bool _isInitialized;

    // Guards the check-and-set of _isInitialized. Initialize() is invoked from
    // several boot paths (bootstrapper, deferred init, HTTPS/port rebuild) that can
    // overlap; without this a plain check-then-set let two callers both pass the
    // guard and spawn duplicate worker sets.
    private readonly object _initializationLock = new();

    private readonly ConcurrentDictionary<string, Thread> _activeWorkerThreads = new();

    private readonly JobQueue _jobQueue;
    public readonly JobDispatcher Dispatcher;
    private readonly IConfigurationStore? _configurationStore;
    private readonly ILogger<QueueRunner> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly NoMercy.NmSystem.Lifecycle.IServerPhaseTracker? _phaseTracker;
    private readonly IResourceBudget? _resourceBudget;
    private readonly IReadOnlySet<string> _resourceAwareQueues;
    private readonly IWorkerActivityGate? _activityGate;
    private readonly IReadOnlyDictionary<
        string,
        NoMercy.NmSystem.Lifecycle.BootStage
    > _queueReadyStages;

    /// <summary>
    /// Static accessor for non-DI code paths (jobs, logic classes).
    /// Set during DI registration, before Initialize() is called.
    /// </summary>
    public static QueueRunner? Current { get; private set; }

    /// <summary>
    /// Exposes the underlying <see cref="JobQueue"/> for coordinator jobs that
    /// need to enqueue continuation work without going through the dispatcher's
    /// deduplication path.
    /// </summary>
    public JobQueue Queue => _jobQueue;

    public QueueRunner(
        IQueueContext queueContext,
        QueueConfiguration configuration,
        ILoggerFactory loggerFactory,
        IConfigurationStore? configurationStore = null,
        IServiceScopeFactory? scopeFactory = null,
        NoMercy.NmSystem.Lifecycle.IServerPhaseTracker? phaseTracker = null,
        IResourceBudget? resourceBudget = null,
        IReadOnlySet<string>? resourceAwareQueues = null,
        IWorkerActivityGate? activityGate = null,
        IReadOnlyDictionary<string, NoMercy.NmSystem.Lifecycle.BootStage>? queueReadyStages = null
    )
    {
        _configurationStore = configurationStore;
        _scopeFactory = scopeFactory;
        _phaseTracker = phaseTracker;
        _resourceBudget = resourceBudget;
        _resourceAwareQueues = resourceAwareQueues ?? new HashSet<string>();
        _activityGate = activityGate;
        _queueReadyStages =
            queueReadyStages ?? new Dictionary<string, NoMercy.NmSystem.Lifecycle.BootStage>();
        _logger = loggerFactory.CreateLogger<QueueRunner>();
        _jobQueue = new(
            context: queueContext,
            maxAttempts: configuration.MaxAttempts,
            logger: loggerFactory.CreateLogger<JobQueue>()
        );
        Dispatcher = new(queue: _jobQueue, logger: loggerFactory.CreateLogger<JobDispatcher>());

        _workers = new();
        foreach (KeyValuePair<string, int> entry in configuration.WorkerCounts)
        {
            _workers[key: entry.Key] = new(count: entry.Value);
        }

        _logger.LogInformation(
            message: "QueueRunner constructed with WorkerCounts: {Counts}",
            args: string.Join(separator: ", ", values: configuration.WorkerCounts.Select(selector: kvp => $"{kvp.Key}={kvp.Value}"))
        );

        Current = this;
    }

    public async Task Initialize()
    {
        lock (_initializationLock)
        {
            if (_isInitialized)
            {
                _logger.LogDebug(message: "QueueRunner.Initialize() skipped — already initialized");
                return;
            }

            _isInitialized = true;
        }

        _jobQueue.ResetAllReservedJobs();

        int workerCount = 0;
        Dictionary<string, int> spawnedPerQueue = new();
        foreach (KeyValuePair<string, WorkerEntry> keyValuePair in _workers)
        {
            int target = keyValuePair.Value.Count;
            for (int i = 0; i < target; i++)
            {
                SpawnWorkerThread(name: keyValuePair.Key);
                workerCount++;
            }
            spawnedPerQueue[key: keyValuePair.Key] = target;
        }

        _logger.LogInformation(
            message: "Queue workers spawned per queue: {Counts} (total {Total})", args: [string.Join(separator: ", ", values: spawnedPerQueue.Select(selector: kvp => $"{kvp.Key}={kvp.Value}")), workerCount]
        );

        // Restore any queues that were persisted as paused before the last shutdown.
        if (_configurationStore is not null)
        {
            List<string> queueNames;
            lock (_workersLock)
            {
                queueNames = [.. _workers.Keys];
            }

            foreach (string queueName in queueNames)
            {
                string key = $"queue.{queueName}.paused";
                if (_configurationStore.HasKey(key: key) && _configurationStore.GetValue(key: key) == "true")
                {
                    await Stop(name: queueName);
                    _logger.LogInformation(
                        message: "Queue '{Name}' restored as paused from configuration store",
                        args: queueName
                    );
                }
            }
        }

        // Signal that queue workers are ready, allowing cron jobs to start execution
        CronWorker.SignalQueueWorkersReady();

        await Task.CompletedTask;
    }

    private void SpawnWorkerThread(string name)
    {
        string threadKey = $"{name}-{Guid.NewGuid():N}";
        Thread thread = new(start: () =>
        {
            try
            {
                SpawnWorker(name: name);
            }
            catch (Exception ex)
            {
                _logger.LogError(message: "Worker {Name} crashed: {Message}", args: [name, ex.Message]);
            }
            finally
            {
                _activeWorkerThreads.TryRemove(key: threadKey, value: out _);
            }
        })
        {
            IsBackground = true,
            Name = $"QueueWorker-{threadKey}",
            Priority = ThreadPriority.Lowest,
        };

        _activeWorkerThreads.TryAdd(key: threadKey, value: thread);
        thread.Start();
    }

    private void SpawnWorker(string name)
    {
        IResourceBudget? budget = _resourceAwareQueues.Contains(item: name) ? _resourceBudget : null;

        QueueWorker queueWorkerInstance = new(
            queue: _jobQueue,
            name: name,
            runner: this,
            scopeFactory: _scopeFactory,
            phaseTracker: _phaseTracker,
            resourceBudget: budget,
            resourceAwareQueues: _resourceAwareQueues,
            activityGate: _activityGate,
            readyStage: _queueReadyStages.TryGetValue(
                key: name,
                value: out NoMercy.NmSystem.Lifecycle.BootStage stage
            )
                ? stage
                : NoMercy.NmSystem.Lifecycle.BootStage.All
        );

        queueWorkerInstance.WorkCompleted += QueueWorkerCompleted(name: name, instance: queueWorkerInstance);

        lock (_workersLock)
        {
            _workers[key: name].WorkerInstances.Add(item: queueWorkerInstance);
        }

        queueWorkerInstance.Start();
    }

    #region MyRegion

    public Task Start(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.. _workers[key: name].WorkerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot)
            workerInstance.Start();

        return Task.CompletedTask;
    }

    public Task StartAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [.. _workers.Keys];
        }

        foreach (string key in keys)
            Start(name: key);

        return Task.CompletedTask;
    }

    public Task Stop(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.. _workers[key: name].WorkerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot)
            workerInstance.Stop();

        return Task.CompletedTask;
    }

    public Task StopAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [.. _workers.Keys];
        }

        foreach (string key in keys)
            Stop(name: key);

        return Task.CompletedTask;
    }

    public Task Restart(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.. _workers[key: name].WorkerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot)
            workerInstance.Restart();

        return Task.CompletedTask;
    }

    public Task RestartAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [.. _workers.Keys];
        }

        foreach (string key in keys)
            Restart(name: key);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop workers for <paramref name="name"/> and persist the paused state so
    /// it survives a server restart. Use this for user-initiated pauses.
    /// </summary>
    public async Task Pause(string name)
    {
        await Stop(name: name);

        if (_configurationStore is not null)
            await _configurationStore.SetValueAsync(key: $"queue.{name}.paused", value: "true");

        _logger.LogInformation(message: "Queue '{Name}' paused and state persisted", args: name);
    }

    /// <summary>
    /// Restart workers for <paramref name="name"/> and clear the persisted paused state.
    /// Use this for user-initiated resumes.
    /// </summary>
    public async Task Resume(string name)
    {
        await Start(name: name);

        if (_configurationStore is not null)
            await _configurationStore.SetValueAsync(key: $"queue.{name}.paused", value: "false");

        _logger.LogInformation(message: "Queue '{Name}' resumed and state persisted", args: name);
    }

    /// <summary>
    /// Persisted paused state for <paramref name="name"/>. Reads from the
    /// configuration store rather than tracking it in-memory so the
    /// dashboard sees the same value that was used to restore the queue at
    /// boot time (otherwise a server restart while paused leaves the UI
    /// showing "running" while the worker is actually stopped).
    /// </summary>
    public bool IsPaused(string name)
    {
        // A queue whose ready stage extends beyond BootStage.All (the encoder
        // queues, which also wait on Hardware detection) reports as paused while
        // that extra stage is still pending. This surfaces the startup hold in
        // the dashboard without persisting it as a user-initiated pause.
        if (
            _queueReadyStages.TryGetValue(key: name, value: out NoMercy.NmSystem.Lifecycle.BootStage readyStage)
        )
        {
            NoMercy.NmSystem.Lifecycle.BootStage extra =
                readyStage & ~NoMercy.NmSystem.Lifecycle.BootStage.All;
            if (
                extra != NoMercy.NmSystem.Lifecycle.BootStage.None
                && _phaseTracker is { } pt
                && !pt.IsComplete(stage: extra)
            )
                return true;
        }

        if (_configurationStore is null)
            return false;

        string key = $"queue.{name}.paused";
        return _configurationStore.HasKey(key: key)
            && string.Equals(
                a: _configurationStore.GetValue(key: key),
                b: "true",
                comparisonType: StringComparison.OrdinalIgnoreCase
            );
    }

    #endregion


    private WorkCompletedEventHandler QueueWorkerCompleted(string name, QueueWorker instance)
    {
        return (_, _) =>
        {
            lock (_workersLock)
            {
                if (!ShouldRemoveWorker(name: name))
                    return;

                instance.Stop();
                _workers[key: name].WorkerInstances.Remove(item: instance);
            }
        };
    }

    private bool ShouldRemoveWorker(string name)
    {
        return _workers[key: name].WorkerInstances.Count > _workers[key: name].Count;
    }

    private void UpdateRunningWorkerCounts(string name)
    {
        int spawned;
        int targetCount;
        CancellationToken token;
        lock (_workersLock)
        {
            if (ShouldRemoveWorker(name: name))
                return;
            spawned = _workers[key: name].WorkerInstances.Count;
            targetCount = _workers[key: name].Count;
            token = _workers[key: name].Cts.Token;
        }

        Task workerTask = Task.Run(
            function: async () =>
            {
                while (spawned < targetCount)
                {
                    bool isUpdating;
                    lock (_workersLock)
                    {
                        isUpdating = _workers[key: name].IsUpdating;
                    }

                    if (isUpdating || spawned >= targetCount)
                        break;

                    SpawnWorkerThread(name: name);
                    spawned += 1;

                    await Task.Delay(millisecondsDelay: 100, cancellationToken: token);
                }
            },
            cancellationToken: token
        );

        workerTask.ContinueWith(
            continuationAction: t =>
                _logger.LogError(
                    message: "UpdateRunningWorkerCounts for {Name} failed: {Message}", args: [name, t.Exception?.GetBaseException().Message]
                ),
            continuationOptions: TaskContinuationOptions.OnlyOnFaulted
        );
    }

    public async Task<bool> SetWorkerCount(string name, int max, Guid? userId)
    {
        bool exists;
        lock (_workersLock)
        {
            exists = _workers.ContainsKey(key: name);
            if (exists && _workers[key: name].Count == max)
                return true;
        }

        if (!exists)
            return false;

        if (_configurationStore is not null)
        {
            await _configurationStore.SetValueAsync(key: $"{name}Runners", value: max.ToString(), modifiedBy: userId);
        }

        _logger.LogInformation(message: "Setting queue {Name} to {Max} workers", args: [name, max]);

        CancellationToken token;
        lock (_workersLock)
        {
            WorkerEntry entry = _workers[key: name];
            entry.IsUpdating = true;
            entry.Cts.Cancel();
            entry.Count = max;
            entry.Cts = new();
            token = entry.Cts.Token;
        }

        await Task.Run(
            action: () =>
            {
                lock (_workersLock)
                {
                    _workers[key: name].IsUpdating = false;
                }
                UpdateRunningWorkerCounts(name: name);
            },
            cancellationToken: token
        );

        return true;
    }

    public int GetWorkerIndex(string name, QueueWorker queueWorker)
    {
        lock (_workersLock)
        {
            return _workers[key: name].WorkerInstances.IndexOf(item: queueWorker);
        }
    }

    public IReadOnlyDictionary<string, Thread> GetActiveWorkerThreads()
    {
        return _activeWorkerThreads;
    }

    /// <summary>
    /// Counts worker instances that are *currently processing a job*
    /// (i.e. <see cref="QueueWorker.IsProcessingJob"/> is true) under
    /// queue names matched by <paramref name="namePredicate"/>.
    ///
    /// <para>This is what callers asking "is the queue busy?" actually
    /// want — <see cref="GetActiveWorkerThreads"/> reports every spawned
    /// thread for its lifetime, including workers blocked on
    /// <c>queue.ReserveJob</c> waiting for the next item, which over-
    /// counts dramatically.</para>
    /// </summary>
    public int CountWorkersProcessingJob(Func<string, bool> namePredicate)
    {
        int count = 0;
        lock (_workersLock)
        {
            foreach (KeyValuePair<string, WorkerEntry> entry in _workers)
            {
                if (!namePredicate(arg: entry.Key))
                    continue;
                foreach (QueueWorker worker in entry.Value.WorkerInstances)
                    if (worker.IsProcessingJob)
                        count++;
            }
        }
        return count;
    }
}
