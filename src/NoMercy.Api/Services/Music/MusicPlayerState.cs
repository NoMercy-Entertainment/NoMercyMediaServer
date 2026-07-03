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

    [JsonProperty("volume_percentage")]
    public int VolumePercentage { get; set; }

    [JsonProperty("device_volumes")]
    public Dictionary<string, int> DeviceVolumes { get; set; } = new();

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
}
