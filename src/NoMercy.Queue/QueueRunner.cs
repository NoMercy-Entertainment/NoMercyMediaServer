using System.Collections.Concurrent;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using NoMercy.Queue.Workers;
using Serilog.Events;

namespace NoMercy.Queue;

public class QueueRunner
{
    private readonly object _workersLock = new();

    private readonly
        Dictionary<string, (int count, List<QueueWorker> workerInstances, CancellationTokenSource _cancellationTokenSource, bool isUpdating)>
        _workers;

    private volatile bool _isInitialized;

    private readonly ConcurrentDictionary<string, Thread> _activeWorkerThreads = new();

    private readonly JobQueue _jobQueue;
    public readonly JobDispatcher Dispatcher;
    private readonly IConfigurationStore? _configurationStore;

    /// <summary>
    /// Static accessor for non-DI code paths (jobs, logic classes).
    /// Set during DI registration, before Initialize() is called.
    /// </summary>
    public static QueueRunner? Current { get; private set; }

    public QueueRunner(IQueueContext queueContext, QueueConfiguration configuration, IConfigurationStore? configurationStore = null)
    {
        _configurationStore = configurationStore;
        _jobQueue = new(queueContext, configuration.MaxAttempts);
        Dispatcher = new(_jobQueue);

        _workers = new();
        foreach (KeyValuePair<string, int> entry in configuration.WorkerCounts)
        {
            _workers[entry.Key] = (entry.Value, [], new(), false);
        }

        Current = this;
    }

    public async Task Initialize()
    {
        if (_isInitialized)
        {
            Logger.Queue("QueueRunner.Initialize() skipped — already initialized", LogEventLevel.Debug);
            return;
        }

        _isInitialized = true;

        Logger.Queue("QueueRunner.Initialize() starting — spawning workers...", LogEventLevel.Information);

        _jobQueue.ResetAllReservedJobs();

        int workerCount = 0;
        foreach (KeyValuePair<string, (int count, List<QueueWorker> workerInstances, CancellationTokenSource
                     _cancellationTokenSource, bool isUpdating)> keyValuePair in _workers)
            for (int i = 0; i < keyValuePair.Value.count; i++)
            {
                SpawnWorkerThread(keyValuePair.Key);
                workerCount++;
            }

        Logger.Queue($"QueueRunner.Initialize() complete — spawned {workerCount} workers", LogEventLevel.Information);

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
                Logger.Queue(
                    $"Worker {name} crashed: {ex.Message}",
                    LogEventLevel.Error);
            }
            finally
            {
                _activeWorkerThreads.TryRemove(threadKey, out _);
            }
        })
        {
            IsBackground = true,
            Name = $"QueueWorker-{threadKey}",
            Priority = ThreadPriority.Lowest
        };

        _activeWorkerThreads.TryAdd(threadKey, thread);
        thread.Start();
    }

    private void SpawnWorker(string name)
    {
        QueueWorker queueWorkerInstance = new(_jobQueue, name, this);

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
            snapshot = [.._workers[name].workerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot) workerInstance.Start();

        return Task.CompletedTask;
    }

    public Task StartAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [.._workers.Keys];
        }

        foreach (string key in keys) Start(key);

        return Task.CompletedTask;
    }

    public Task Stop(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.._workers[name].workerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot) workerInstance.Stop();

        return Task.CompletedTask;
    }

    public Task StopAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [.._workers.Keys];
        }

        foreach (string key in keys) Stop(key);

        return Task.CompletedTask;
    }

    public Task Restart(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [.._workers[name].workerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot) workerInstance.Restart();

        return Task.CompletedTask;
    }

    public Task RestartAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [.._workers.Keys];
        }

        foreach (string key in keys) Restart(key);

        return Task.CompletedTask;
    }

    #endregion


    private WorkCompletedEventHandler QueueWorkerCompleted(string name, QueueWorker instance)
    {
        return (_, _) =>
        {
            lock (_workersLock)
            {
                if (!ShouldRemoveWorker(name)) return;

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
            if (ShouldRemoveWorker(name)) return;
            spawned = _workers[name].workerInstances.Count;
            targetCount = _workers[name].count;
            token = _workers[name]._cancellationTokenSource.Token;
        }

        Task workerTask = Task.Run(async () =>
        {
            while (spawned < targetCount)
            {
                bool isUpdating;
                lock (_workersLock)
                {
                    isUpdating = _workers[name].isUpdating;
                }

                if (isUpdating || spawned >= targetCount) break;

                SpawnWorkerThread(name);
                spawned += 1;

                await Task.Delay(100, token);
            }
        }, token);

        workerTask.ContinueWith(
            t => Logger.Queue(
                $"UpdateRunningWorkerCounts for {name} failed: {t.Exception?.GetBaseException().Message}",
                LogEventLevel.Error),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task<bool> SetWorkerCount(string name, int max, Guid? userId)
    {
        bool exists;
        lock (_workersLock)
        {
            exists = _workers.ContainsKey(name);
            if (exists && _workers[name].count == max) return true;
        }

        if (!exists) return false;

        if (_configurationStore is not null)
        {
            await _configurationStore.SetValueAsync($"{name}Runners", max.ToString(), userId);
        }

        Logger.Queue($"Setting queue {name} to {max} workers", LogEventLevel.Information);

        CancellationToken token;
        lock (_workersLock)
        {
            (int count, List<QueueWorker> workerInstances, CancellationTokenSource _cancellationTokenSource, bool isUpdating)
                valueTuple = _workers[name];
            valueTuple.isUpdating = true;
            valueTuple._cancellationTokenSource.Cancel();
            valueTuple.count = max;
            valueTuple._cancellationTokenSource = new();
            _workers[name] = valueTuple;
            token = valueTuple._cancellationTokenSource.Token;
        }

        await Task.Run(() =>
        {
            lock (_workersLock)
            {
                (int count, List<QueueWorker> workerInstances, CancellationTokenSource _cancellationTokenSource, bool isUpdating)
                    valueTuple = _workers[name];
                valueTuple.isUpdating = false;
                _workers[name] = valueTuple;
            }
            UpdateRunningWorkerCounts(name);
        }, token);

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
}
