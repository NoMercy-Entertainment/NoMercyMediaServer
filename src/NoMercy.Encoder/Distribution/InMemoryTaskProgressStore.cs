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

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Thread-safe in-memory store. LRU eviction after <see cref="MaxEntries"/>
/// and age-based eviction after <see cref="StaleAfter"/> keep the map
/// bounded when workers disconnect mid-task.
/// </summary>
public class InMemoryTaskProgressStore : ITaskProgressStore
{
    private const int MaxEntries = 500;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, TaskProgressSnapshot> _snapshots = new();

    public void Update(string taskId, TaskProgressSnapshot snapshot)
    {
        _snapshots[taskId] = snapshot;

        // Evict stale entries lazily on every write to avoid a background
        // cleanup thread. Cheap: bounded-size concurrent dict + one linq
        // pass per update.
        if (_snapshots.Count > MaxEntries)
            EvictStale();
    }

    public TaskProgressSnapshot? Get(string taskId) =>
        _snapshots.TryGetValue(taskId, out TaskProgressSnapshot? snap) ? snap : null;

    public IReadOnlyList<TaskProgressSnapshot> GetAll()
    {
        DateTime cutoff = DateTime.UtcNow - StaleAfter;
        return _snapshots.Values.Where(s => s.ReceivedAtUtc >= cutoff).ToArray();
    }

    private void EvictStale()
    {
        DateTime cutoff = DateTime.UtcNow - StaleAfter;
        foreach (
            KeyValuePair<string, TaskProgressSnapshot> kvp in _snapshots
                .Where(kvp => kvp.Value.ReceivedAtUtc < cutoff)
                .ToArray()
        )
        {
            _snapshots.TryRemove(kvp.Key, out _);
        }
    }
}
