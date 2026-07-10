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

    // Per-user monotonic, clock-anchored broadcast sequence — assigned once per
    // emit by MusicPlaybackService.UpdatePlaybackState via
    // MusicPlayerStateManager.NextSeq, never authored here. Lets a client that
    // receives broadcasts out of order (many ungated call sites can all fire
    // within milliseconds of each other) drop any state whose Seq is not
    // greater than the last one it applied, instead of racing position/
    // play-state fields against each other. New additive field: old clients
    // that don't read it are unaffected.
    [JsonProperty("seq")]
    public long Seq { get; set; }

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
    /// queue entry (<see cref="Playlist"/> and <see cref="Backlog"/>) drops the
    /// three fields no client renders from a queue row — the track
    /// <see cref="PlaylistTrackDto.Lyrics"/> sheet, the track
    /// <see cref="PlaylistTrackDto.ColorPalette"/> swatch graph, and the same
    /// palette plus the unbounded description on each nested
    /// <see cref="PlaylistTrackDto.Album"/> / <see cref="PlaylistTrackDto.Artist"/>
    /// entry. A queue row shows only cover, name, and album/artist names; the
    /// current track's own theming comes from <see cref="CurrentItem"/> (kept
    /// whole) and each list screen fetches its palette from REST. On a long
    /// queue (e.g. a whole genre) every position broadcast — throttled to ~5s,
    /// so each one carries the entire playlist + backlog — otherwise re-sends a
    /// palette-and-lyric graph per track. That multi-MB payload's flush, and the
    /// receiving device's re-parse of it every tick, is the measured cause of
    /// remote-action lag and, on a memory-tight client, of GC thrash that kills
    /// the playback service and forces an activity restart. The stored state is
    /// never mutated — this returns a throwaway shallow copy whose queue lists
    /// hold stripped record copies (so a queue entry that shares a reference
    /// with <see cref="CurrentItem"/> cannot strip it), and the nested album/
    /// artist copies are fresh clones so their in-place null-outs never reach
    /// the stored DTOs.
    /// </summary>
    public MusicPlayerState CloneForBroadcast()
    {
        MusicPlayerState copy = (MusicPlayerState)MemberwiseClone();
        copy.Playlist = Playlist.Select(StripQueueEntry).ToList();
        copy.Backlog = Backlog.Select(StripQueueEntry).ToList();
        return copy;
    }

    private static PlaylistTrackDto StripQueueEntry(PlaylistTrackDto track) =>
        track with
        {
            Lyrics = null,
            ColorPalette = null,
            Album = track.Album.Select(album => album.ForBroadcastQueueEntry()).ToList(),
            Artist = track.Artist.Select(artist => artist.ForBroadcastQueueEntry()).ToList(),
        };
}
