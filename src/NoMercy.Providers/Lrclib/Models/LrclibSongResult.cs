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
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "trackName")]
    public string TrackName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "artistName")]
    public string ArtistName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "albumName")]
    public string AlbumName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "duration")]
    public double Duration { get; set; }

    [JsonProperty(propertyName: "instrumental")]
    public bool Instrumental { get; set; }

    [JsonProperty(propertyName: "plainLyrics")]
    public string PlainLyrics { get; set; } = string.Empty;

    [JsonProperty(propertyName: "syncedLyrics")]
    public string SyncedLyrics { get; set; } = string.Empty;

    // Error handling
    [JsonProperty(propertyName: "message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty(propertyName: "statusCode")]
    public int StatusCode { get; set; } = 200;
}
