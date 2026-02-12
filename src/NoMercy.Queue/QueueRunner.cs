using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Workers;
using Serilog.Events;

namespace NoMercy.Queue;

public static class QueueRunner
{
    private static readonly object _workersLock = new();

    private static readonly
        Dictionary<string, (int count, List<QueueWorker> workerInstances, CancellationTokenSource _cancellationTokenSource)>
        Workers = new()
        {
            [Config.QueueWorkers.Key] = (Config.QueueWorkers.Value, [], new()),
            [Config.EncoderWorkers.Key] = (Config.EncoderWorkers.Value, [], new()),
            [Config.CronWorkers.Key] = (Config.CronWorkers.Value, [], new()),
            [Config.DataWorkers.Key] = (Config.DataWorkers.Value, [], new()),
            [Config.ImageWorkers.Key] = (Config.ImageWorkers.Value, [], new())
            //[Config.RequestRunners.Key] = (Config.RequestRunners.Value, [], new CancellationTokenSource()),
        };

    private static volatile bool _isInitialized;
    private static readonly JobQueue JobQueue = new(new EfQueueContextAdapter(new()));
    private static volatile bool _isUpdating;

    private static readonly ConcurrentDictionary<string, Thread> ActiveWorkerThreads = new();

    public static async Task Initialize()
    {
        if (_isInitialized) return;

        _isInitialized = true;

        await using QueueContext queueContext = new();
        await queueContext.QueueJobs
            .ForEachAsync(job => job.ReservedAt = null);
        await queueContext.SaveChangesAsync();

        foreach (KeyValuePair<string, (int count, List<QueueWorker> workerInstances, CancellationTokenSource
                     _cancellationTokenSource)> keyValuePair in Workers)
            for (int i = 0; i < keyValuePair.Value.count; i++)
                SpawnWorkerThread(keyValuePair.Key);

        // Signal that queue workers are ready, allowing cron jobs to start execution
        CronWorker.SignalQueueWorkersReady();
    }

    private static void SpawnWorkerThread(string name)
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
                ActiveWorkerThreads.TryRemove(threadKey, out _);
            }
        })
        {
            IsBackground = true,
            Name = $"QueueWorker-{threadKey}",
            Priority = ThreadPriority.Lowest
        };

        ActiveWorkerThreads.TryAdd(threadKey, thread);
        thread.Start();
    }

    private static void SpawnWorker(string name)
    {
        QueueWorker queueWorkerInstance = new(JobQueue, name);

        queueWorkerInstance.WorkCompleted += QueueWorkerCompleted(name, queueWorkerInstance);

        lock (_workersLock)
        {
            Workers[name].workerInstances.Add(queueWorkerInstance);
        }

        queueWorkerInstance.Start();
    }


    #region MyRegion

    public static Task Start(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [..Workers[name].workerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot) workerInstance.Start();

        return Task.CompletedTask;
    }

    public static Task StartAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [..Workers.Keys];
        }

        foreach (string key in keys) Start(key);

        return Task.CompletedTask;
    }

    public static Task Stop(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [..Workers[name].workerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot) workerInstance.Stop();

        return Task.CompletedTask;
    }

    public static Task StopAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [..Workers.Keys];
        }

        foreach (string key in keys) Stop(key);

        return Task.CompletedTask;
    }

    public static Task Restart(string name)
    {
        List<QueueWorker> snapshot;
        lock (_workersLock)
        {
            snapshot = [..Workers[name].workerInstances];
        }

        foreach (QueueWorker workerInstance in snapshot) workerInstance.Restart();

        return Task.CompletedTask;
    }

    public static Task RestartAll()
    {
        List<string> keys;
        lock (_workersLock)
        {
            keys = [..Workers.Keys];
        }

        foreach (string key in keys) Restart(key);

        return Task.CompletedTask;
    }

    #endregion


    private static WorkCompletedEventHandler QueueWorkerCompleted(string name, QueueWorker instance)
    {
        return (_, _) =>
        {
            lock (_workersLock)
            {
                if (!ShouldRemoveWorker(name)) return;

                instance.Stop();
                Workers[name].workerInstances.Remove(instance);
            }
        };
    }

    private static bool ShouldRemoveWorker(string name)
    {
        return Workers[name].workerInstances.Count > Workers[name].count;
    }

    private static void UpdateRunningWorkerCounts(string name)
    {
        CancellationToken token;
        lock (_workersLock)
        {
            if (ShouldRemoveWorker(name)) return;
            token = Workers[name]._cancellationTokenSource.Token;
        }

        Task workerTask = Task.Run(async () =>
        {
            while (!_isUpdating)
            {
                int currentCount;
                int targetCount;
                lock (_workersLock)
                {
                    currentCount = Workers[name].workerInstances.Count;
                    targetCount = Workers[name].count;
                }

                if (_isUpdating || currentCount >= targetCount) break;

                SpawnWorkerThread(name);

                await Task.Delay(100, token);
            }
        }, token);

        workerTask.ContinueWith(
            t => Logger.Queue(
                $"UpdateRunningWorkerCounts for {name} failed: {t.Exception?.GetBaseException().Message}",
                LogEventLevel.Error),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public static async Task<bool> SetWorkerCount(string name, int max, Guid? userId)
    {
        bool exists;
        lock (_workersLock)
        {
            exists = Workers.ContainsKey(name);
        }

        if (!exists) return false;

        await using MediaContext context = new();
        await context.Configuration
            .Upsert(new()
            {
                Key = $"{name}Runners",
                ModifiedBy = userId,
                Value = max.ToString()
            })
            .On(x => x.Key)
            .WhenMatched((s, i) => new()
            {
                Value = max.ToString(),
                ModifiedBy = i.ModifiedBy
            })
            .RunAsync();

        Logger.Queue($"Setting queue {name} to {max} workers", LogEventLevel.Information);

        CancellationToken token;
        lock (_workersLock)
        {
            _isUpdating = true;
            Workers[name]._cancellationTokenSource.Cancel();

            (int count, List<QueueWorker> workerInstances, CancellationTokenSource _cancellationTokenSource)
                valueTuple = Workers[name];
            valueTuple.count = max;
            valueTuple._cancellationTokenSource = new();
            Workers[name] = valueTuple;
            token = valueTuple._cancellationTokenSource.Token;
        }

        await Task.Run(() =>
        {
            _isUpdating = false;
            UpdateRunningWorkerCounts(name);
        }, token);

        return true;
    }

    public static int GetWorkerIndex(string name, QueueWorker queueWorker)
    {
        lock (_workersLock)
        {
            return Workers[name].workerInstances.IndexOf(queueWorker);
        }
    }

    public static IReadOnlyDictionary<string, Thread> GetActiveWorkerThreads()
    {
        return ActiveWorkerThreads;
    }
}
