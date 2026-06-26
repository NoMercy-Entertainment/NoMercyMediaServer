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
    [JsonProperty("budget")]
    public int Budget { get; set; }

    [JsonProperty("genres")]
    public TmdbGenre[] Genres { get; set; } = [];

    [JsonProperty("homepage")]
    public Uri? Homepage { get; set; }

    [JsonProperty("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty("revenue")]
    public long Revenue { get; set; }

    [JsonProperty("runtime")]
    public int Runtime { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("production_companies")]
    public TmdbProductionCompany[] ProductionCompanies { get; set; } = [];

    [JsonProperty("belongs_to_collection")]
    public BelongsToCollection? BelongsToCollection { get; set; }

    [JsonProperty("production_countries")]
    public TmdbProductionCountry[] ProductionCountries { get; set; } = [];

    [JsonProperty("spoken_languages")]
    public TmdbSpokenLanguage[] SpokenLanguages { get; set; } = [];
}
