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
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Hubs.Shared;
using NoMercy.Database.Models.Media;

namespace NoMercy.Api.Services.Video;

public class VideoPlayerState
{
    [JsonProperty("actions")]
    public Actions Actions { get; set; } = null!;

    [JsonProperty("device_id")]
    public string? DeviceId { get; set; }

    [JsonProperty("is_playing")]
    public bool PlayState { get; set; }

    [JsonProperty("item")]
    public VideoPlaylistResponseDto? CurrentItem { get; set; }

    [JsonProperty("playlist")]
    public List<VideoPlaylistResponseDto> Playlist { get; set; } = [];

    [JsonProperty("progress_ms")]
    public int Time { get; set; }

    [JsonProperty("duration_ms")]
    public int Duration { get; set; }

    [JsonProperty("current_list")]
    public Uri CurrentList { get; set; } = null!;

    [JsonProperty("muted_state")]
    public bool Muted { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("volume_percentage")]
    public int VolumePercentage { get; set; }

    [JsonProperty("seek_offset")]
    public int SeekOffset { get; set; }

    [JsonProperty("current_caption")]
    public ISubtitle? CurrentCaption { get; set; }

    [JsonProperty("current_audio")]
    public IAudio? CurrentAudio { get; set; }

    [JsonProperty("current_quality")]
    public IVideo? CurrentQuality { get; set; }

    // Server-internal cast/remote-control lists (chapter skip, cycle audio/
    // caption/quality). Sourced from the current item's Metadata at build time;
    // never serialized — clients read the equivalents from tracks[] / the HLS
    // manifest.
    [JsonIgnore]
    public List<IChapter> Chapters { get; set; } = [];

    [JsonIgnore]
    public List<IAudio> Audio { get; set; } = [];

    [JsonIgnore]
    public List<ISubtitle> Captions { get; set; } = [];

    [JsonIgnore]
    public List<IVideo> Qualities { get; set; } = [];
}
