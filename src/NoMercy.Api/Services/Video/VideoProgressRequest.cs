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
    [JsonProperty("app")]
    public int AppId { get; set; }

    [JsonProperty("video_id")]
    public Ulid VideoId { get; set; }

    [JsonProperty("tmdb_id")]
    public int TmdbId { get; set; }

    [JsonProperty("playlist_type")]
    public string PlaylistType { get; set; } = string.Empty;

    [JsonProperty("video_type")]
    public string VideoType { get; set; } = string.Empty;

    [JsonProperty("time")]
    public int Time { get; set; }

    [JsonProperty("audio")]
    public string Audio { get; set; } = string.Empty;

    [JsonProperty("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonProperty("subtitle_type")]
    public string SubtitleType { get; set; } = string.Empty;

    [JsonProperty("special_id")]
    public Ulid? SpecialId { get; set; }

    [JsonProperty("collection_id")]
    public int? CollectionId { get; set; }

    [JsonProperty("playlist_id")]
    public dynamic PlaylistId { get; set; } = null!;
}
