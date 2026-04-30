using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;

namespace NoMercyQueue;

public class QueueRunner
{
    private readonly object _workersLock = new();

    private readonly Dictionary<
        string,
        (
            int count,
            List<QueueWorker> workerInstances,
            CancellationTokenSource _cancellationTokenSource,
            bool isUpdating
        )
    > _workers;

    private volatile bool _isInitialized;

    private readonly ConcurrentDictionary<string, Thread> _activeWorkerThreads = new();

    private readonly JobQueue _jobQueue;
    public readonly JobDispatcher Dispatcher;
    private readonly IConfigurationStore? _configurationStore;
    private readonly ILogger<QueueRunner> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Static accessor for non-DI code paths (jobs, logic classes).
    /// Set during DI registration, before Initialize() is called.
    /// </summary>
    public static QueueRunner? Current { get; private set; }

    public QueueRunner(
        IQueueContext queueContext,
        QueueConfiguration configuration,
        ILoggerFactory loggerFactory,
        IConfigurationStore? configurationStore = null,
        IServiceScopeFactory? scopeFactory = null
    )
    {
        _configurationStore = configurationStore;
        _scopeFactory = scopeFactory;
        _logger = loggerFactory.CreateLogger<QueueRunner>();
        _jobQueue = new(
            queueContext,
            configuration.MaxAttempts,
            loggerFactory.CreateLogger<JobQueue>()
        );
        Dispatcher = new(_jobQueue, loggerFactory.CreateLogger<JobDispatcher>());

        _workers = new();
        foreach (KeyValuePair<string, int> entry in configuration.WorkerCounts)
        {
            _workers[entry.Key] = (entry.Value, [], new(), false);
        }

        _logger.LogInformation(
            "QueueRunner constructed with WorkerCounts: {Counts}",
            string.Join(", ", configuration.WorkerCounts.Select(kvp => $"{kvp.Key}={kvp.Value}"))
        );

        Current = this;
    }

    public async Task Initialize()
    {
        if (_isInitialized)
        {
            _logger.LogDebug("QueueRunner.Initialize() skipped — already initialized");
            return;
        }

        _isInitialized = true;

        _jobQueue.ResetAllReservedJobs();

        int workerCount = 0;
        Dictionary<string, int> spawnedPerQueue = new();
        foreach (
            KeyValuePair<
                string,
                (
                    int count,
                    List<QueueWorker> workerInstances,
                    CancellationTokenSource _cancellationTokenSource,
                    bool isUpdating
                )
            > keyValuePair in _workers
        )
        {
            int target = keyValuePair.Value.count;
            for (int i = 0; i < target; i++)
            {
                SpawnWorkerThread(keyValuePair.Key);
                workerCount++;
            }
            spawnedPerQueue[keyValuePair.Key] = target;
        }

        _logger.LogInformation(
            "Queue workers spawned per queue: {Counts} (total {Total})",
            string.Join(", ", spawnedPerQueue.Select(kvp => $"{kvp.Key}={kvp.Value}")),
            workerCount
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
                if (_configurationStore.HasKey(key) && _configurationStore.GetValue(key) == "true")
                {
                    await Stop(queueName);
                    _logger.LogInformation(
                        "Queue '{Name}' restored as paused from configuration store",
                        queueName
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
        Thread thread = new(() =>
        {
            try
            {
                SpawnWorker(name);
            }
            catch (Exception ex)
            {
                _logger.LogError("Worker {Name} crashed: {Message}", name, ex.Message);
            }
            finally
            {
                _activeWorkerThreads.TryRemove(threadKey, out _);
            }
        })
        {
            IsBackground = true,
            Name = $"QueueWorker-{threadKey}",
            Priority = ThreadPriority.Lowest,
        };

        _activeWorkerThreads.TryAdd(threadKey, thread);
        thread.Start();
    }

    private void SpawnWorker(string name)
    {
        QueueWorker queueWorkerInstance = new(_jobQueue, name, this, scopeFactory: _scopeFactory);

        queueWorkerInstance.WorkCompleted += QueueWorkerCompleted(name, queueWorkerInstance);

        lock (_workersLock)
        {
            _workers[name].workerInstances.Add(queueWorkerInstance);
        }

        queueWorkerInstance.Start();
    }

    #region MyRegion

    public Task Start(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.. _workers[name].workerInstances];
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
            Start(key);

        return Task.CompletedTask;
    }

    public Task Stop(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.. _workers[name].workerInstances];
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
            Stop(key);

        return Task.CompletedTask;
    }

    public Task Restart(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.. _workers[name].workerInstances];
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
            Restart(key);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop workers for <paramref name="name"/> and persist the paused state so
    /// it survives a server restart. Use this for user-initiated pauses.
    /// </summary>
    public async Task Pause(string name)
    {
        await Stop(name);

        if (_configurationStore is not null)
            await _configurationStore.SetValueAsync($"queue.{name}.paused", "true");

        _logger.LogInformation("Queue '{Name}' paused and state persisted", name);
    }

    /// <summary>
    /// Restart workers for <paramref name="name"/> and clear the persisted paused state.
    /// Use this for user-initiated resumes.
    /// </summary>
    public async Task Resume(string name)
    {
        await Start(name);

        if (_configurationStore is not null)
            await _configurationStore.SetValueAsync($"queue.{name}.paused", "false");

        _logger.LogInformation("Queue '{Name}' resumed and state persisted", name);
    }

    #endregion


    private WorkCompletedEventHandler QueueWorkerCompleted(string name, QueueWorker instance)
    {
        return (_, _) =>
        {
            lock (_workersLock)
            {
                if (!ShouldRemoveWorker(name))
                    return;

                instance.Stop();
                _workers[name].workerInstances.Remove(instance);
            }
        };
    }

    private bool ShouldRemoveWorker(string name)
    {
        return _workers[name].workerInstances.Count > _workers[name].count;
    }

    private void UpdateRunningWorkerCounts(string name)
    {
        int spawned;
        int targetCount;
        CancellationToken token;
        lock (_workersLock)
        {
            if (ShouldRemoveWorker(name))
                return;
            spawned = _workers[name].workerInstances.Count;
            targetCount = _workers[name].count;
            token = _workers[name]._cancellationTokenSource.Token;
        }

        Task workerTask = Task.Run(
            async () =>
            {
                while (spawned < targetCount)
                {
                    bool isUpdating;
                    lock (_workersLock)
                    {
                        isUpdating = _workers[name].isUpdating;
                    }

                    if (isUpdating || spawned >= targetCount)
                        break;

                    SpawnWorkerThread(name);
                    spawned += 1;

                    await Task.Delay(100, token);
                }
            },
            token
        );

        workerTask.ContinueWith(
            t =>
                _logger.LogError(
                    "UpdateRunningWorkerCounts for {Name} failed: {Message}",
                    name,
                    t.Exception?.GetBaseException().Message
                ),
            TaskContinuationOptions.OnlyOnFaulted
        );
    }

    public async Task<bool> SetWorkerCount(string name, int max, Guid? userId)
    {
        bool exists;
        lock (_workersLock)
        {
            exists = _workers.ContainsKey(name);
            if (exists && _workers[name].count == max)
                return true;
        }

        if (!exists)
            return false;

        if (_configurationStore is not null)
        {
            await _configurationStore.SetValueAsync($"{name}Runners", max.ToString(), userId);
        }

        _logger.LogInformation("Setting queue {Name} to {Max} workers", name, max);

        CancellationToken token;
        lock (_workersLock)
        {
            (
                int count,
                List<QueueWorker> workerInstances,
                CancellationTokenSource _cancellationTokenSource,
                bool isUpdating
            ) valueTuple = _workers[name];
            valueTuple.isUpdating = true;
            valueTuple._cancellationTokenSource.Cancel();
            valueTuple.count = max;
            valueTuple._cancellationTokenSource = new();
            _workers[name] = valueTuple;
            token = valueTuple._cancellationTokenSource.Token;
        }

        await Task.Run(
            () =>
            {
                lock (_workersLock)
                {
                    (
                        int count,
                        List<QueueWorker> workerInstances,
                        CancellationTokenSource _cancellationTokenSource,
                        bool isUpdating
                    ) valueTuple = _workers[name];
                    valueTuple.isUpdating = false;
                    _workers[name] = valueTuple;
                }
                UpdateRunningWorkerCounts(name);
            },
            token
        );

        return true;
    }

    public int GetWorkerIndex(string name, QueueWorker queueWorker)
    {
        lock (_workersLock)
        {
            return _workers[name].workerInstances.IndexOf(queueWorker);
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
            foreach (
                KeyValuePair<
                    string,
                    (
                        int count,
                        List<QueueWorker> workerInstances,
                        CancellationTokenSource _,
                        bool isUpdating
                    )
                > entry in _workers
            )
            {
                if (!namePredicate(entry.Key))
                    continue;
                foreach (QueueWorker worker in entry.Value.workerInstances)
                    if (worker.IsProcessingJob)
                        count++;
            }
        }
        return count;
    }
}
