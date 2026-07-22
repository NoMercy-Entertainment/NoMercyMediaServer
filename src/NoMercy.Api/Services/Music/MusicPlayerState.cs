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
    [JsonProperty(propertyName: "actions")]
    public Actions Actions { get; set; } = new();

    [JsonProperty(propertyName: "device_id")]
    public string? DeviceId { get; set; }

    [JsonProperty(propertyName: "is_playing")]
    public bool PlayState { get; set; }

    [JsonProperty(propertyName: "item")]
    public PlaylistTrackDto? CurrentItem { get; set; }

    [JsonProperty(propertyName: "playlist")]
    public List<PlaylistTrackDto> Playlist { get; set; } = [];

    [JsonProperty(propertyName: "backlog")]
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

    [JsonProperty(propertyName: "current_list")]
    public Uri CurrentList { get; set; } = null!;

    [JsonProperty(propertyName: "progress_ms")]
    public int Time { get; set; }

    [JsonProperty(propertyName: "duration_ms")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "repeat_state")]
    public string Repeat { get; set; } = "off";

    [JsonProperty(propertyName: "shuffle_state")]
    public bool Shuffle { get; set; }

    [JsonProperty(propertyName: "muted_state")]
    public bool Muted { get; set; }

    [JsonProperty(propertyName: "timestamp")]
    public long Timestamp { get; set; }

    // Epoch ms the server clock read at the instant this broadcast left
    // MusicPlaybackService.UpdatePlaybackState. Distinct from Timestamp (which
    // predates this field and some clients may already depend on for other
    // purposes): this is the reference instant for GetServerTime-style clock-
    // offset math, letting every device compute the shared server clock and
    // therefore the same playback position regardless of its own wall-clock skew.
    [JsonProperty(propertyName: "server_time_ms")]
    public long ServerTimeMs { get; set; }

    // Epoch ms the server clock read at the instant Time was last authored —
    // an accepted position report, a seek, a device transfer, or a track
    // change — never on the internal 100ms playback tick (which advances Time
    // in lockstep with wall time and needs no fresh reference point). Paired
    // with Time and ServerTimeMs, a client derives the live position via
    // Time + (serverNow - PositionCapturedAtMs) without waiting on its own
    // report cadence. Old clients that ignore this field are unaffected.
    [JsonProperty(propertyName: "position_captured_at_ms")]
    public long PositionCapturedAtMs { get; set; }

    // Per-user monotonic, clock-anchored broadcast sequence — assigned once per
    // emit by MusicPlaybackService.UpdatePlaybackState via
    // MusicPlayerStateManager.NextSeq, never authored here. Lets a client that
    // receives broadcasts out of order (many ungated call sites can all fire
    // within milliseconds of each other) drop any state whose Seq is not
    // greater than the last one it applied, instead of racing position/
    // play-state fields against each other. New additive field: old clients
    // that don't read it are unaffected.
    [JsonProperty(propertyName: "seq")]
    public long Seq { get; set; }

    [JsonProperty(propertyName: "volume_percentage")]
    public int VolumePercentage { get; set; }

    // Case-insensitive: device ids are compared OrdinalIgnoreCase everywhere else in
    // MusicHub, and this map is keyed by the same ids (ResolveTransferVolume,
    // ApplyDeviceVolume). A case-sensitive map would silently miss a remembered
    // volume the moment a caller's casing didn't match the one that first wrote it.
    [JsonProperty(propertyName: "device_volumes")]
    public Dictionary<string, int> DeviceVolumes { get; set; } =
        new(comparer: StringComparer.OrdinalIgnoreCase);

    [JsonProperty(propertyName: "seek_offset")]
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

    // The broadcast carries only a window of the queue, not the whole thing.
    // Playlist is the UPCOMING queue (front = the very next track), Backlog is
    // history (back = most recent) — see MusicPlaybackCommandHandler's advance
    // logic — so a front-of-Playlist / back-of-Backlog slice is exactly the
    // "up next" + "just played" a client renders. The server keeps the FULL
    // lists in its own state for auto-advance and previous; only the wire copy
    // is bounded. A genre queue is ~8k tracks: serializing all of them into
    // every ~5s position broadcast cost ~2s per emit server-side, which landed
    // each play/pause broadcast seconds late and let an interleaved stale tick
    // snap the client's play-state and position backward (the measured "pause
    // skips back and resumes"). The full list stays available on the REST
    // /music/{type}/{id} endpoint the client already loads its list screens from.
    private const int BroadcastPlaylistWindow = 100;
    private const int BroadcastBacklogWindow = 20;

    /// <summary>
    /// A lightweight serialization projection for the client broadcast. Two
    /// reductions, both keyed off the fact that a whole-genre queue is thousands
    /// of tracks re-sent on every ~5s position broadcast:
    /// <list type="number">
    /// <item>The queue is windowed — <see cref="Playlist"/> to the next
    /// <see cref="BroadcastPlaylistWindow"/> upcoming tracks and
    /// <see cref="Backlog"/> to the last <see cref="BroadcastBacklogWindow"/> —
    /// which is what caps the per-emit serialize cost (and thus remote-action
    /// latency).</item>
    /// <item>Every windowed entry drops the fields no client renders from a
    /// queue row — the track <see cref="PlaylistTrackDto.Lyrics"/> sheet, the
    /// track <see cref="PlaylistTrackDto.ColorPalette"/> swatch graph, and the
    /// same palette plus the unbounded description on each nested
    /// <see cref="PlaylistTrackDto.Album"/> / <see cref="PlaylistTrackDto.Artist"/>
    /// entry — which is what curbs a memory-tight client's GC thrash.</item>
    /// </list>
    /// A queue row shows only cover, name, and album/artist names; the current
    /// track's own theming comes from <see cref="CurrentItem"/> (kept whole) and
    /// each list screen fetches its palette from REST. The stored state is never
    /// mutated — this returns a throwaway shallow copy whose queue lists hold
    /// stripped record copies (so a queue entry that shares a reference with
    /// <see cref="CurrentItem"/> cannot strip it), and the nested album/artist
    /// copies are fresh clones so their in-place null-outs never reach the
    /// stored DTOs.
    /// </summary>
    public MusicPlayerState CloneForBroadcast()
    {
        MusicPlayerState copy = (MusicPlayerState)MemberwiseClone();
        copy.Playlist = Playlist.Take(count: BroadcastPlaylistWindow).Select(selector: StripQueueEntry).ToList();
        copy.Backlog = Backlog.TakeLast(count: BroadcastBacklogWindow).Select(selector: StripQueueEntry).ToList();
        return copy;
    }

    private static PlaylistTrackDto StripQueueEntry(PlaylistTrackDto track) =>
        track with
        {
            Lyrics = null,
            ColorPalette = null,
            Album = track.Album.Select(selector: album => album.ForBroadcastQueueEntry()).ToList(),
            Artist = track.Artist.Select(selector: artist => artist.ForBroadcastQueueEntry()).ToList(),
        };
}
