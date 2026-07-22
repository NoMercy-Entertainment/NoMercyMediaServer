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
using NoMercy.Providers.TMDB.Models.Combined;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonAppends : TmdbSeasonDetails
{
    [JsonProperty(propertyName: "aggregate_credits")]
    public TmdbSeasonAggregatedCredits AggregateCredits { get; set; } = new();

    [JsonProperty(propertyName: "changes")]
    public TmdbSeasonChanges? Changes { get; set; }

    [JsonProperty(propertyName: "credits")]
    public TmdbSeasonCredits TmdbSeasonCredits { get; set; } = new();

    [JsonProperty(propertyName: "external_ids")]
    public TmdbSeasonExternalIds TmdbSeasonExternalIds { get; set; } = new();

    [JsonProperty(propertyName: "images")]
    public TmdbSeasonImages TmdbSeasonImages { get; set; } = new();

    [JsonProperty(propertyName: "translations")]
    public TmdbCombinedTranslations Translations { get; set; } = new();

    [JsonProperty(propertyName: "videos")]
    public TmdbSeasonVideos TmdbSeasonVideos { get; set; } = new();
}
