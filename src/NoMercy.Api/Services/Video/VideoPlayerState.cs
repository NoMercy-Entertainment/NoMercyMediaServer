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
    [JsonProperty(propertyName: "actions")]
    public Actions Actions { get; set; } = null!;

    [JsonProperty(propertyName: "device_id")]
    public string? DeviceId { get; set; }

    [JsonProperty(propertyName: "is_playing")]
    public bool PlayState { get; set; }

    [JsonProperty(propertyName: "item")]
    public VideoPlaylistResponseDto? CurrentItem { get; set; }

    [JsonProperty(propertyName: "playlist")]
    public List<VideoPlaylistResponseDto> Playlist { get; set; } = [];

    [JsonProperty(propertyName: "progress_ms")]
    public int Time { get; set; }

    [JsonProperty(propertyName: "duration_ms")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "current_list")]
    public Uri CurrentList { get; set; } = null!;

    [JsonProperty(propertyName: "muted_state")]
    public bool Muted { get; set; }

    [JsonProperty(propertyName: "timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty(propertyName: "volume_percentage")]
    public int VolumePercentage { get; set; }

    [JsonProperty(propertyName: "seek_offset")]
    public int SeekOffset { get; set; }

    [JsonProperty(propertyName: "current_caption")]
    public ISubtitle? CurrentCaption { get; set; }

    [JsonProperty(propertyName: "current_audio")]
    public IAudio? CurrentAudio { get; set; }

    [JsonProperty(propertyName: "current_quality")]
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
