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

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Binds a live session's lifetime to the SignalR connection watching it. A
/// browser that hard-navigates, closes its tab, or drops off the network never
/// runs the graceful DELETE teardown, so without this the session (and its
/// ffmpeg) would linger until the idle reaper's multi-minute timeout — stacking
/// up against the concurrency cap. Here a connection close disposes the session
/// after a short grace window that a reconnect cancels.
/// </summary>
public interface ILiveSessionPresenceTracker
{
    /// <summary>
    /// A connection began (or resumed) watching <paramref name="sessionId"/>.
    /// Cancels any pending grace-window disposal from a prior connection drop.
    /// </summary>
    void OnSubscribed(string connectionId, string sessionId);

    /// <summary>
    /// A connection explicitly stopped watching <paramref name="sessionId"/>
    /// (graceful player teardown). Does not itself dispose — the caller's DELETE
    /// owns that — it just stops the disconnect handler from also acting on it.
    /// </summary>
    void OnUnsubscribed(string connectionId, string sessionId);

    /// <summary>
    /// A connection closed. Every session it was still watching is scheduled for
    /// disposal once the grace window elapses without a reconnect re-subscribing.
    /// </summary>
    void OnConnectionClosed(string connectionId);
}
