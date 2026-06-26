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

namespace NoMercy.Providers.Lrclib.Models;

[Serializable]
public class LrclibSongResult
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("trackName")]
    public string TrackName { get; set; } = string.Empty;

    [JsonProperty("artistName")]
    public string ArtistName { get; set; } = string.Empty;

    [JsonProperty("albumName")]
    public string AlbumName { get; set; } = string.Empty;

    [JsonProperty("duration")]
    public double Duration { get; set; }

    [JsonProperty("instrumental")]
    public bool Instrumental { get; set; }

    [JsonProperty("plainLyrics")]
    public string PlainLyrics { get; set; } = string.Empty;

    [JsonProperty("syncedLyrics")]
    public string SyncedLyrics { get; set; } = string.Empty;

    // Error handling
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("statusCode")]
    public int StatusCode { get; set; } = 200;
}
