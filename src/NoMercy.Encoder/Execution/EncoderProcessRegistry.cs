namespace NoMercy.Encoder.Execution;

using System.Collections.Concurrent;

public class EncoderProcessRegistry : IEncoderProcessRegistry
{
    private readonly ConcurrentDictionary<int, HashSet<int>> _processes = new();
    private readonly object _lock = new();

    public void Register(int jobId, int processId)
    {
        if (processId <= 0)
            return;

        lock (_lock)
        {
            if (!_processes.TryGetValue(jobId, out HashSet<int>? set))
            {
                set = [];
                _processes[jobId] = set;
            }
            set.Add(processId);
        }
    }

    public void Unregister(int jobId, int processId)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(jobId, out HashSet<int>? set))
                return;

            set.Remove(processId);
            if (set.Count == 0)
                _processes.TryRemove(jobId, out _);
        }
    }

    public void UnregisterJob(int jobId)
    {
        lock (_lock)
        {
            _processes.TryRemove(jobId, out _);
        }
    }

    public IReadOnlyCollection<int> GetProcessIds(int jobId)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(jobId, out HashSet<int>? set))
                return [];
            return set.ToArray();
        }
    }

    public IReadOnlyCollection<int> ActiveJobIds
    {
        get
        {
            lock (_lock)
            {
                return _processes.Keys.ToArray();
            }
        }
    }
}
