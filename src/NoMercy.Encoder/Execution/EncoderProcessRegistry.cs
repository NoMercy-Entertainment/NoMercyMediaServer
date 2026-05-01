using System.Collections.Concurrent;

namespace NoMercy.Encoder.Execution;

public class EncoderProcessRegistry : IEncoderProcessRegistry
{
    private readonly ConcurrentDictionary<int, HashSet<int>> _processes = new();

    // Stores argv per pid so CountConcurrentNvencSessions can inspect codec flags.
    // Only populated via RegisterWithArgv — entries registered via Register() alone
    // are not present here and are therefore not counted as NVENC sessions.
    private readonly ConcurrentDictionary<int, string[]> _argvByPid = new();

    private readonly object _lock = new();

    private static readonly string[] NvencCodecFlags = ["h264_nvenc", "hevc_nvenc", "av1_nvenc"];

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

    public void RegisterWithArgv(int jobId, int processId, string[] argv)
    {
        if (processId <= 0)
            return;

        Register(jobId, processId);
        _argvByPid[processId] = argv;
    }

    public void Unregister(int jobId, int processId)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(jobId, out HashSet<int>? set))
                return;

            set.Remove(processId);
            _argvByPid.TryRemove(processId, out _);

            if (set.Count == 0)
                _processes.TryRemove(jobId, out _);
        }
    }

    public void UnregisterJob(int jobId)
    {
        lock (_lock)
        {
            if (_processes.TryRemove(jobId, out HashSet<int>? pids))
            {
                foreach (int pid in pids)
                    _argvByPid.TryRemove(pid, out _);
            }
        }
    }

    public int CountConcurrentNvencSessions()
    {
        int count = 0;

        foreach (KeyValuePair<int, string[]> entry in _argvByPid)
        {
            string[] argv = entry.Value;

            foreach (string flag in NvencCodecFlags)
            {
                bool found = false;

                foreach (string arg in argv)
                {
                    if (
                        string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)
                        || arg.Contains(flag, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
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
