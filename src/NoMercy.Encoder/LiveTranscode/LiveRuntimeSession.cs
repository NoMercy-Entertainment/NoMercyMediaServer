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
using System.Globalization;
using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.LiveTranscode;

public sealed class LiveRuntimeSession : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, Segment> _segments = new();
    private readonly CancellationTokenSource _drainerCts = new();
    private int _highestIndex = -1;
    private int _isComplete;
    private long _lastAccessTicks = DateTime.UtcNow.Ticks;
    private int _epoch;

    public ILiveSession Session { get; }
    public TimeSpan TargetSegmentDuration { get; }
    public string? ScratchDirectory { get; }

    /// <summary>
    /// The media info originally analyzed for this session. Retained so the
    /// quality-change endpoint can enumerate available qualities without
    /// re-probing the file.
    /// </summary>
    public MediaInfo? CachedMediaInfo { get; internal set; }

    /// <summary>
    /// The client capabilities supplied when the session was started. Retained
    /// alongside <see cref="CachedMediaInfo"/> for the same reason.
    /// </summary>
    public ClientCapabilities? ClientCapabilities { get; internal set; }

    /// <summary>
    /// Monotonic generation counter for the segment buffer. Incremented on every
    /// buffer reset (seek or quality change) so clients can tell — via the epoch
    /// embedded in segment URLs — that indices from a previous generation are now
    /// stale and must be discarded.
    /// </summary>
    public string CurrentEpoch => Volatile.Read(ref _epoch).ToString(CultureInfo.InvariantCulture);

    internal Task? DrainerTask { get; set; }

    public LiveRuntimeSession(
        ILiveSession session,
        TimeSpan targetSegmentDuration,
        string? scratchDirectory = null
    )
    {
        Session = session;
        TargetSegmentDuration = targetSegmentDuration;
        ScratchDirectory = scratchDirectory;
    }

    public bool IsComplete => Volatile.Read(ref _isComplete) == 1;

    /// <summary>
    /// UTC time of the last playlist or segment access. Used by the idle reaper.
    /// </summary>
    public DateTime LastAccess => new(Interlocked.Read(ref _lastAccessTicks), DateTimeKind.Utc);

    /// <summary>
    /// Updates <see cref="LastAccess"/> to now. Called from playlist and segment
    /// serve points so the reaper knows the session is still in use.
    /// </summary>
    public void TouchLastAccess() =>
        Interlocked.Exchange(ref _lastAccessTicks, DateTime.UtcNow.Ticks);

    public int HighestSegmentIndex => Volatile.Read(ref _highestIndex);

    public bool TryGetSegment(int index, out Segment segment) =>
        _segments.TryGetValue(index, out segment!);

    /// <summary>
    /// Snapshot of buffered segments ordered by index. Safe to call concurrently
    /// with the drainer — callers see whatever segments were buffered at the
    /// moment of the snapshot.
    /// </summary>
    public IReadOnlyList<Segment> SnapshotSegments()
    {
        return _segments.Values.OrderBy(s => s.Index).ToList();
    }

    internal CancellationToken DrainerCancellation => _drainerCts.Token;

    internal void BufferSegment(Segment segment)
    {
        _segments[segment.Index] = segment;

        int current;
        do
        {
            current = Volatile.Read(ref _highestIndex);
            if (segment.Index <= current)
                break;
        } while (Interlocked.CompareExchange(ref _highestIndex, segment.Index, current) != current);
    }

    internal void MarkComplete() => Interlocked.Exchange(ref _isComplete, 1);

    internal void ResetBuffer()
    {
        _segments.Clear();
        Volatile.Write(ref _highestIndex, -1);
        // Bump the generation so segment URLs minted before this reset (e.g. a
        // pre-seek playlist the client cached) are recognised as stale.
        Interlocked.Increment(ref _epoch);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _drainerCts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        if (DrainerTask is not null)
        {
            try
            {
                await DrainerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        await Session.DisposeAsync().ConfigureAwait(false);
        _drainerCts.Dispose();
    }
}
