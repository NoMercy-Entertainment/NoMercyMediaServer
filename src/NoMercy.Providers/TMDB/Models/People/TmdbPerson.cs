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

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPerson
{
    [JsonProperty("birthday")]
    public DateTime? BirthDay { get; set; }

    [JsonProperty("known_for_department")]
    public string? KnownForDepartment { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("also_known_as")]
    public string[] AlsoKnownAs { get; set; } = [];

    [JsonProperty("gender")]
    public TmdbGender TmdbGender { get; set; } = TmdbGender.Unknown;

    [JsonProperty("biography")]
    public string? Biography { get; set; }

    [JsonProperty("popularity")]
    public double Popularity { get; set; }

    [JsonProperty("place_of_birth")]
    public string? PlaceOfBirth { get; set; }

    [JsonProperty("profile_path")]
    public string? ProfilePath { get; set; }

    [JsonProperty("adult")]
    public bool Adult { get; set; }

    [JsonProperty("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty("external_ids")]
    public Database.Models.People.TmdbPersonExternalIds? ExternalIds { get; set; }
}
