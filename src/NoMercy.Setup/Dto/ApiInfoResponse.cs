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

namespace NoMercy.Setup.Dto;

public class ApiInfoResponse
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "data")]
    public Data Data { get; set; } = new();

    [JsonProperty(propertyName: "_cached_at")]
    public string? CachedAt { get; set; }
}

public class Data
{
    [JsonProperty(propertyName: "state")]
    public string State { get; set; } = string.Empty;

    [JsonProperty(propertyName: "version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty(propertyName: "copyright")]
    public string Copyright { get; set; } = string.Empty;

    [JsonProperty(propertyName: "licence")]
    public string Licence { get; set; } = string.Empty;

    [JsonProperty(propertyName: "contact")]
    public Contact Contact { get; set; } = new();

    [JsonProperty(propertyName: "git")]
    public Uri? Git { get; set; }

    [JsonProperty(propertyName: "keys")]
    public Keys Keys { get; set; } = new();

    [JsonProperty(propertyName: "quote")]
    public string Quote { get; set; } = string.Empty;

    [JsonProperty(propertyName: "colors")]
    public string[] Colors { get; set; } = [];
}

public class Socials
{
    [JsonProperty(propertyName: "twitch")]
    public Uri? Twitch { get; set; }

    [JsonProperty(propertyName: "youtube")]
    public Uri? Youtube { get; set; }

    [JsonProperty(propertyName: "twitter")]
    public Uri? Twitter { get; set; }

    [JsonProperty(propertyName: "discord")]
    public string Discord { get; set; } = string.Empty;
}

public class Keys
{
    [JsonProperty(propertyName: "make_mkv_key")]
    public string MakeMkvKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tmdb_key")]
    public string TmdbKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "omdb_key")]
    public string OmdbKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "fanart_key")]
    public string FanArtKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "rotten_tomatoes")]
    public string RottenTomatoes { get; set; } = string.Empty;

    [JsonProperty(propertyName: "acoustic_id_key")]
    public string AcousticIdKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tadb_key")]
    public string TadbKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tmdb_token")]
    public string TmdbToken { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tvdb_key")]
    public string TvdbKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "musixmatch_key")]
    public string MusixmatchKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "jwplayer_key")]
    public string JwplayerKey { get; set; } = string.Empty;
}

public class Contact
{
    [JsonProperty(propertyName: "homepage")]
    public string Homepage { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty(propertyName: "dmca")]
    public string Dmca { get; set; } = string.Empty;

    [JsonProperty(propertyName: "languages")]
    public string Languages { get; set; } = string.Empty;

    [JsonProperty(propertyName: "socials")]
    public Socials Socials { get; set; } = new();
}
