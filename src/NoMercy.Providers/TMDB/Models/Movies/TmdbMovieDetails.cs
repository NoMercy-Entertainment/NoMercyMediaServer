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
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieDetails : TmdbMovie
{
    [JsonProperty(propertyName: "budget")]
    public int Budget { get; set; }

    [JsonProperty(propertyName: "genres")]
    public TmdbGenre[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "homepage")]
    public Uri? Homepage { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty(propertyName: "revenue")]
    public long Revenue { get; set; }

    [JsonProperty(propertyName: "runtime")]
    public int Runtime { get; set; }

    [JsonProperty(propertyName: "status")]
    public string? Status { get; set; }

    [JsonProperty(propertyName: "production_companies")]
    public TmdbProductionCompany[] ProductionCompanies { get; set; } = [];

    [JsonProperty(propertyName: "belongs_to_collection")]
    public BelongsToCollection? BelongsToCollection { get; set; }

    [JsonProperty(propertyName: "production_countries")]
    public TmdbProductionCountry[] ProductionCountries { get; set; } = [];

    [JsonProperty(propertyName: "spoken_languages")]
    public TmdbSpokenLanguage[] SpokenLanguages { get; set; } = [];
}
