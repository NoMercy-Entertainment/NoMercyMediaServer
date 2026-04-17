namespace NoMercy.Encoder.Distribution;

using System.Collections.Concurrent;
using NoMercy.Encoder.Jobs;

/// <summary>
/// Thread-safe registry populated at runtime by the worker-registration
/// API. Workers self-register after booting, send periodic heartbeats to
/// stay "active", and drop off when they miss two consecutive intervals.
/// The dispatcher reads a snapshot of currently-active workers on every
/// dispatch — no subscription / streaming, so registration churn doesn't
/// affect mid-job dispatches.
///
/// Stale entries are pruned lazily on reads: <see cref="GetActiveWorkers"/>
/// filters out anything past the stale threshold and drops them from the
/// dictionary so stopped workers don't accumulate forever.
/// </summary>
public class InMemoryRemoteWorkerRegistry : IRemoteWorkerRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredWorker> _workers = new();
    private readonly TimeSpan _staleAfter;
    private readonly Func<DateTime> _clock;

    public InMemoryRemoteWorkerRegistry(TimeSpan? staleAfter = null, Func<DateTime>? clock = null)
    {
        _staleAfter = staleAfter ?? TimeSpan.FromSeconds(60);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Adds a new worker or refreshes an existing one. Idempotent — workers
    /// that re-register after a restart replace their own entry.
    /// </summary>
    public void Register(IRemoteWorker worker)
    {
        _workers[worker.WorkerId] = new RegisteredWorker(worker, _clock());
    }

    /// <summary>
    /// Marks the worker as still alive. Returns false if the worker isn't
    /// registered (caller should treat as "re-register required").
    /// </summary>
    public bool Heartbeat(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out RegisteredWorker? existing))
            return false;

        _workers[workerId] = existing with { LastSeenUtc = _clock() };
        return true;
    }

    /// <summary>
    /// Removes a worker explicitly — called when a worker shuts down
    /// cleanly so the dispatcher stops trying to reach it.
    /// </summary>
    public bool Unregister(string workerId) => _workers.TryRemove(workerId, out _);

    public IReadOnlyList<IRemoteWorker> GetActiveWorkers()
    {
        DateTime now = _clock();
        DateTime cutoff = now - _staleAfter;

        // Evict stale entries lazily — keeps the dictionary from growing
        // unbounded when workers come and go.
        foreach (
            KeyValuePair<string, RegisteredWorker> kvp in _workers
                .Where(kvp => kvp.Value.LastSeenUtc < cutoff)
                .ToArray()
        )
        {
            _workers.TryRemove(kvp.Key, out _);
        }

        return _workers.Values.Select(rw => rw.Worker).ToArray();
    }

    private sealed record RegisteredWorker(IRemoteWorker Worker, DateTime LastSeenUtc);
}
