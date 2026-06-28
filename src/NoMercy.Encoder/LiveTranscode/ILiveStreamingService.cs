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

using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveStreamingService
{
    /// <summary>
    /// Registers a live session, starting a background task that drains its
    /// segment channel into an indexed buffer so any segment can be served
    /// non-sequentially by id. When <paramref name="scratchDirectory"/> is
    /// provided, <see cref="RemoveAsync"/> will best-effort delete it after
    /// disposing the session.
    /// </summary>
    void Register(
        ILiveSession session,
        TimeSpan targetSegmentDuration,
        string? scratchDirectory = null
    );

    /// <summary>
    /// Stores the original media analysis context on an already-registered
    /// runtime so the quality-change endpoint can enumerate available qualities
    /// without re-probing the file.
    /// </summary>
    void StampRequestContext(string sessionId, MediaInfo mediaInfo, ClientCapabilities client);

    bool TryGetRuntime(string sessionId, out LiveRuntimeSession runtime);

    Task RemoveAsync(string sessionId);

    /// <summary>
    /// True when <paramref name="sessionId"/> was registered earlier and has
    /// since been removed (ended, evicted, or expired) within the tombstone
    /// window. Lets the API layer answer 410 Gone for a once-valid session
    /// rather than 404 Not Found for one that never existed.
    /// </summary>
    bool WasRecentlyRemoved(string sessionId);

    IReadOnlyCollection<string> ActiveSessionIds { get; }

    /// <summary>
    /// Returns a point-in-time snapshot of all currently registered live sessions.
    /// Safe to call concurrently — enumerates the underlying dictionary under a
    /// consistent view and projects each runtime into an immutable snapshot record.
    /// </summary>
    IReadOnlyList<LiveSessionSnapshot> GetActiveSessions();
}
