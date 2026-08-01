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
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("data")]
    public Data Data { get; set; } = new();

    [JsonProperty("_cached_at")]
    public string? CachedAt { get; set; }
}

public class Data
{
    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("copyright")]
    public string Copyright { get; set; } = string.Empty;

    [JsonProperty("licence")]
    public string Licence { get; set; } = string.Empty;

    [JsonProperty("contact")]
    public Contact Contact { get; set; } = new();

    [JsonProperty("git")]
    public Uri? Git { get; set; }

    [JsonProperty("keys")]
    public Keys Keys { get; set; } = new();

    [JsonProperty("quote")]
    public string Quote { get; set; } = string.Empty;

    [JsonProperty("colors")]
    public string[] Colors { get; set; } = [];
}

public class Socials
{
    [JsonProperty("twitch")]
    public Uri? Twitch { get; set; }

    [JsonProperty("youtube")]
    public Uri? Youtube { get; set; }

    [JsonProperty("twitter")]
    public Uri? Twitter { get; set; }

    [JsonProperty("discord")]
    public string Discord { get; set; } = string.Empty;
}

public class Keys
{
    [JsonProperty("makemkv_key")]
    public string MakeMkvKey { get; set; } = string.Empty;

    [JsonProperty("tmdb_key")]
    public string TmdbKey { get; set; } = string.Empty;

    [JsonProperty("omdb_key")]
    public string OmdbKey { get; set; } = string.Empty;

    [JsonProperty("fanart_key")]
    public string FanArtKey { get; set; } = string.Empty;

    [JsonProperty("rotten_tomatoes")]
    public string RottenTomatoes { get; set; } = string.Empty;

    [JsonProperty("acoustic_id")]
    public string AcousticIdKey { get; set; } = string.Empty;

    [JsonProperty("tadb_key")]
    public string TadbKey { get; set; } = string.Empty;

    [JsonProperty("tmdb_token")]
    public string TmdbToken { get; set; } = string.Empty;

    [JsonProperty("tvdb_key")]
    public string TvdbKey { get; set; } = string.Empty;

    [JsonProperty("musixmatch_key")]
    public string MusixmatchKey { get; set; } = string.Empty;
}

public class Contact
{
    [JsonProperty("homepage")]
    public string Homepage { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("dmca")]
    public string Dmca { get; set; } = string.Empty;

    [JsonProperty("languages")]
    public string Languages { get; set; } = string.Empty;

    [JsonProperty("socials")]
    public Socials Socials { get; set; } = new();
}
