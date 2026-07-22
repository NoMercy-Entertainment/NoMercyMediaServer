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
using NoMercy.NmSystem.Monitoring;
using NoMercyQueue;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Bridges <see cref="MediaActivityMonitor"/> (the media-server's playback
/// signal) into the generic <see cref="IWorkerActivityGate"/> the queue
/// library consults.
///
/// <para>
/// Only "library" and "file" actually walk the filesystem/NAS directly
/// (recursive directory scans, ffprobe against real media bytes) — those are
/// the only queues that genuinely compete with a stream for NAS I/O. "import"
/// and "extras" are overwhelmingly API/DB work (TMDB fetch + EF writes) and
/// must always be free to run, playback or not — a user adding a show/movie
/// while watching something else isn't putting any load on the server. The
/// encoder and music queues are likewise never deferred.
/// </para>
///
/// <para>
/// Even for library/file, deferral is capped: a queue is only held back for
/// <see cref="MaxDeferIntervalDefault"/> before one poll is let through
/// regardless of playback. A long-running viewing session must never fully
/// starve a scan — it should just trickle instead of running at full speed.
/// </para>
/// </summary>
public class MediaPlaybackActivityGate : IWorkerActivityGate
{
    private const int DeferIntervalSeconds = 2;

    private static readonly TimeSpan DeferIntervalValue = TimeSpan.FromSeconds(
        seconds: DeferIntervalSeconds
    );

    private static readonly TimeSpan MaxDeferIntervalDefault = TimeSpan.FromSeconds(seconds: 30);

    private static readonly HashSet<string> NasReadHeavyQueues = new(
        comparer: StringComparer.Ordinal
    )
    {
        "library",
        "file",
    };

    private readonly MediaActivityMonitor _monitor;
    private readonly TimeSpan _maxDeferInterval;
    private readonly Func<DateTime> _utcNow;
    private readonly ConcurrentDictionary<string, DateTime> _lastAllowedUtc = new(
        comparer: StringComparer.Ordinal
    );

    public MediaPlaybackActivityGate(MediaActivityMonitor monitor)
        : this(monitor, maxDeferInterval: null, utcNow: null) { }

    /// <summary>
    /// Test-only seam: lets tests shrink <paramref name="maxDeferInterval"/>
    /// and fake the clock instead of sleeping 30 real seconds.
    /// </summary>
    internal MediaPlaybackActivityGate(
        MediaActivityMonitor monitor,
        TimeSpan? maxDeferInterval,
        Func<DateTime>? utcNow
    )
    {
        _monitor = monitor;
        _maxDeferInterval = maxDeferInterval ?? MaxDeferIntervalDefault;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public bool ShouldDefer(string queueName)
    {
        if (!_monitor.IsActive || !NasReadHeavyQueues.Contains(item: queueName))
            return false;

        DateTime now = _utcNow();
        DateTime lastAllowed = _lastAllowedUtc.GetOrAdd(queueName, now);

        if (now - lastAllowed < _maxDeferInterval)
            return true;

        _lastAllowedUtc[queueName] = now;
        return false;
    }

    public TimeSpan DeferInterval => DeferIntervalValue;
}
