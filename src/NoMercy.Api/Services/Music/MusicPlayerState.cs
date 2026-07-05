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

using Newtonsoft.Json;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Hubs.Shared;

namespace NoMercy.Api.Services.Music;

public class MusicPlayerState
{
    [JsonProperty("actions")]
    public Actions Actions { get; set; } = new();

    [JsonProperty("device_id")]
    public string? DeviceId { get; set; }

    [JsonProperty("is_playing")]
    public bool PlayState { get; set; }

    [JsonProperty("item")]
    public PlaylistTrackDto? CurrentItem { get; set; }

    [JsonProperty("playlist")]
    public List<PlaylistTrackDto> Playlist { get; set; } = [];

    [JsonProperty("backlog")]
    public List<PlaylistTrackDto> Backlog { get; set; } = [];

    // [JsonProperty("playlist")]
    // public List<PlaylistTrackDto> Playlist
    // {
    //     get => field.Take(20).ToList();
    //     set;
    // } = [];
    //
    // [JsonProperty("backlog")]
    // public List<PlaylistTrackDto> Backlog
    // {
    //     get => field.Take(20).ToList();
    //     set;
    // } = [];

    [JsonProperty("current_list")]
    public Uri CurrentList { get; set; } = null!;

    [JsonProperty("progress_ms")]
    public int Time { get; set; }

    [JsonProperty("duration_ms")]
    public int Duration { get; set; }

    [JsonProperty("repeat_state")]
    public string Repeat { get; set; } = "off";

    [JsonProperty("shuffle_state")]
    public bool Shuffle { get; set; }

    [JsonProperty("muted_state")]
    public bool Muted { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    // Epoch ms the server clock read at the instant this broadcast left
    // MusicPlaybackService.UpdatePlaybackState. Distinct from Timestamp (which
    // predates this field and some clients may already depend on for other
    // purposes): this is the reference instant for GetServerTime-style clock-
    // offset math, letting every device compute the shared server clock and
    // therefore the same playback position regardless of its own wall-clock skew.
    [JsonProperty("server_time_ms")]
    public long ServerTimeMs { get; set; }

    // Epoch ms the server clock read at the instant Time was last authored —
    // an accepted position report, a seek, a device transfer, or a track
    // change — never on the internal 100ms playback tick (which advances Time
    // in lockstep with wall time and needs no fresh reference point). Paired
    // with Time and ServerTimeMs, a client derives the live position via
    // Time + (serverNow - PositionCapturedAtMs) without waiting on its own
    // report cadence. Old clients that ignore this field are unaffected.
    [JsonProperty("position_captured_at_ms")]
    public long PositionCapturedAtMs { get; set; }

    [JsonProperty("volume_percentage")]
    public int VolumePercentage { get; set; }

    // Case-insensitive: device ids are compared OrdinalIgnoreCase everywhere else in
    // MusicHub, and this map is keyed by the same ids (ResolveTransferVolume,
    // ApplyDeviceVolume). A case-sensitive map would silently miss a remembered
    // volume the moment a caller's casing didn't match the one that first wrote it.
    [JsonProperty("device_volumes")]
    public Dictionary<string, int> DeviceVolumes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonProperty("seek_offset")]
    public int SeekOffset { get; set; }

    [JsonIgnore]
    public DateTime IgnoreCurrentTimeUntil { get; set; }

    [JsonIgnore]
    public bool CrossfadeSignalSent { get; set; }

    // Set to true by CrossfadeStartCommand; suppresses server auto-advance during the
    // client's crossfade window.  Cleared by CrossfadeCompleteCommand or the safety timeout.
    [JsonIgnore]
    public bool IsCrossfading { get; set; }

    // The UTC deadline by which CrossfadeComplete must arrive before the server forces advance.
    // Equals DateTime.UtcNow + fadeDuration + CrossfadeSafetyMarginMs at the time CrossfadeStart
    // is received.
    [JsonIgnore]
    public DateTime CrossfadeTimeout { get; set; }

    // The DeviceId that sent CrossfadeStart.  Only that device may send CrossfadeComplete or
    // cancel the suppression, preventing multi-device conflicts.
    [JsonIgnore]
    public string? CrossfadeDeviceId { get; set; }

    // Proof-of-life clock for the active device (DeviceId above). Refreshed by
    // MusicHub.ReportPositionCommand while playing and by
    // MusicPlaybackService.StartPlaybackTimer on every (re)start of the ticking
    // loop (a resume after a long pause must not look instantly stale). Defaults
    // to "now" so a freshly-created session gets a full grace window before its
    // first position report is due. See MusicPlaybackService.IsActiveDeviceStale.
    [JsonIgnore]
    public DateTime LastActiveHeartbeatUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The single choke point for authoring Time: sets the position and stamps
    /// <see cref="PositionCapturedAtMs"/> to server-now in the same call, so the
    /// two can never travel out of sync. Every seek, track change, and accepted
    /// position report must go through this rather than assigning
    /// <see cref="Time"/> directly.
    /// </summary>
    public void SetPosition(int positionMs)
    {
        Time = positionMs;
        PositionCapturedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// A lightweight serialization projection for the client broadcast: every
    /// queue entry (<see cref="Playlist"/> and <see cref="Backlog"/>) drops its
    /// <see cref="PlaylistTrackDto.Lyrics"/>. No client renders lyrics from a
    /// queue item — lyrics are shown only for the current track, and both the
    /// Android and web clients also fetch the current track's lyrics from the
    /// REST lyrics endpoint — so a full lyric sheet per queued track is pure
    /// wire weight. On a long queue that bloats every position broadcast
    /// (throttled to ~5s, so each one carries the whole playlist + backlog)
    /// enough that its flush and the receiving device's parse block prompt
    /// delivery of the next command: the measured cause of remote-action lag.
    /// <see cref="CurrentItem"/> keeps its lyrics for instant render. The stored
    /// state is never mutated — this returns a throwaway shallow copy whose
    /// queue lists hold lyric-stripped record copies (so a queue entry that
    /// shares a reference with <see cref="CurrentItem"/> cannot strip it).
    /// </summary>
    public MusicPlayerState CloneForBroadcast()
    {
        MusicPlayerState copy = (MusicPlayerState)MemberwiseClone();
        copy.Playlist = Playlist.Select(track => track with { Lyrics = null }).ToList();
        copy.Backlog = Backlog.Select(track => track with { Lyrics = null }).ToList();
        return copy;
    }
}
