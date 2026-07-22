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
using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Execution;

public class EncoderProcessRegistry : IEncoderProcessRegistry
{
    private readonly ConcurrentDictionary<int, HashSet<int>> _processes = new();

    // Stores argv per pid so CountConcurrentNvencSessions can inspect codec flags.
    // Only populated via RegisterWithArgv — entries registered via Register() alone
    // are not present here and are therefore not counted as NVENC sessions.
    private readonly ConcurrentDictionary<int, string[]> _argvByPid = new();

    private readonly object _lock = new();

    private static readonly IReadOnlyList<string> NvencCodecFlags = GpuEncoderTokens.NvencNames;

    public void Register(int jobId, int processId)
    {
        if (processId <= 0)
            return;

        lock (_lock)
        {
            if (!_processes.TryGetValue(key: jobId, value: out HashSet<int>? set))
            {
                set = [];
                _processes[key: jobId] = set;
            }

            set.Add(item: processId);
        }
    }

    public void RegisterWithArgv(int jobId, int processId, string[] argv)
    {
        if (processId <= 0)
            return;

        Register(jobId: jobId, processId: processId);
        _argvByPid[key: processId] = argv;
    }

    public void Unregister(int jobId, int processId)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(key: jobId, value: out HashSet<int>? set))
                return;

            set.Remove(item: processId);
            _argvByPid.TryRemove(key: processId, value: out _);

            if (set.Count == 0)
                _processes.TryRemove(key: jobId, value: out _);
        }
    }

    public void UnregisterJob(int jobId)
    {
        lock (_lock)
        {
            if (_processes.TryRemove(key: jobId, value: out HashSet<int>? pids))
            {
                foreach (int pid in pids)
                    _argvByPid.TryRemove(key: pid, value: out _);
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
                        string.Equals(a: arg, b: flag, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || arg.Contains(value: flag, comparisonType: StringComparison.OrdinalIgnoreCase)
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
            if (!_processes.TryGetValue(key: jobId, value: out HashSet<int>? set))
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
