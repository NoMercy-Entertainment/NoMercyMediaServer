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

public class LiveSessionLimits
{
    public int MaxConcurrentSessions { get; set; } = 4;
    public int MaxSessionsPerUser { get; set; } = 2;
    public long MaxSegmentDiskUsageBytes { get; set; } = 1L * 1024 * 1024 * 1024;
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Minutes of inactivity (no playlist or segment hit) after which the reaper
    /// disposes and cleans the session. Default: 5 minutes.
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Seconds to wait after a watching client's SignalR connection closes before
    /// disposing its session. Long enough for an automatic reconnect (network
    /// blip, brief route change) to re-subscribe and keep playback alive; short
    /// enough that a real tap-out frees the concurrency slot promptly. Default: 15s.
    /// </summary>
    public int DisconnectGraceSeconds { get; set; } = 15;

    /// <summary>
    /// Buffer thresholds used by <see cref="BufferManager"/> to decide whether to
    /// suspend, resume, or drop quality on a live transcode. All in seconds of
    /// buffer-ahead measured from the player's reported playhead.
    /// </summary>
    public BufferThresholds Buffer { get; set; } = new();
}

public sealed class BufferThresholds
{
    /// <summary>Above this many seconds ahead, suspend the encoder.</summary>
    public int SuspendAboveSeconds { get; set; } = 30;

    /// <summary>Below this many seconds (while suspended), resume.</summary>
    public int ResumeBelowSeconds { get; set; } = 15;

    /// <summary>Below this many seconds, drop one quality tier.</summary>
    public int DropQualityBelowSeconds { get; set; } = 5;

    /// <summary>Below this many seconds, drop straight to the lowest tier.</summary>
    public int EmergencyDropBelowSeconds { get; set; } = 3;

    /// <summary>
    /// Below this many seconds of client-reported download-buffer depth
    /// (<see cref="ILiveSession.ClientBufferedAhead"/>), drop straight to the
    /// lowest quality tier regardless of the encoder-lead signal — the client
    /// is draining toward a stall. Default: 2s.
    /// </summary>
    public int ClientEmergencyStallSeconds { get; set; } = 2;

    /// <summary>
    /// Fraction of the client's observed downlink treated as usable capacity
    /// when fitting a quality tier to bandwidth (the rest is safety margin for
    /// HTTP/TLS overhead and other traffic sharing the link). Applied as
    /// <c>observedBandwidthKbps * UsableBandwidthFraction</c>. Default: 0.8.
    /// </summary>
    public double UsableBandwidthFraction { get; set; } = 0.8;

    /// <summary>
    /// Above this many seconds of client-reported download-buffer depth, the
    /// client is healthy enough to be considered for a quality raise. Default: 10s.
    /// </summary>
    public int RaiseHealthyBufferSeconds { get; set; } = 10;

    /// <summary>
    /// Number of consecutive buffer-adaptive sweeps the downlink must
    /// comfortably sustain a higher tier (and the client buffer stay healthy)
    /// before a quality raise fires — hysteresis to prevent flapping. At the
    /// service's 5s sweep interval, 3 sweeps is roughly 15s. Default: 3.
    /// </summary>
    public int RaiseSustainSweeps { get; set; } = 3;

    /// <summary>
    /// A client-health report (<see cref="ILiveSession.HasFreshClientHealth"/>)
    /// older than this is treated as absent, so the network axis is skipped and
    /// behavior falls back to the encoder-lead-only path — this is what keeps an
    /// old client that never reports buffer health working exactly as before.
    /// Default: 10s.
    /// </summary>
    public int ClientHealthStalenessSeconds { get; set; } = 10;
}
