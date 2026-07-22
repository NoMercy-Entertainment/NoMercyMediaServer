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

namespace NoMercy.Api.Services.Video;

public class VideoProgressRequest
{
    [JsonProperty(propertyName: "app")]
    public int AppId { get; set; }

    [JsonProperty(propertyName: "video_id")]
    public Ulid VideoId { get; set; }

    [JsonProperty(propertyName: "tmdb_id")]
    public int TmdbId { get; set; }

    [JsonProperty(propertyName: "playlist_type")]
    public string PlaylistType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "video_type")]
    public string VideoType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "time")]
    public int Time { get; set; }

    [JsonProperty(propertyName: "audio")]
    public string Audio { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtitle_type")]
    public string SubtitleType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "special_id")]
    public Ulid? SpecialId { get; set; }

    [JsonProperty(propertyName: "collection_id")]
    public int? CollectionId { get; set; }

    [JsonProperty(propertyName: "playlist_id")]
    public dynamic PlaylistId { get; set; } = null!;
}
