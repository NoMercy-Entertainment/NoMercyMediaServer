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

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeAppends : TmdbEpisodeDetails
{
    [JsonProperty(propertyName: "credits")]
    public TmdbEpisodeCredits TmdbEpisodeCredits { get; set; } = new();

    [JsonProperty(propertyName: "changes")]
    public TmdbEpisodeChanges Changes { get; set; } = new();

    [JsonProperty(propertyName: "external_ids")]
    public TmdbEpisodeExternalIds TmdbEpisodeExternalIds { get; set; } = new();

    [JsonProperty(propertyName: "images")]
    public TmdbEpisodeImages TmdbEpisodeImages { get; set; } = new();

    [JsonProperty(propertyName: "translations")]
    public TmdbCombinedTranslations Translations { get; set; } = new();

    [JsonProperty(propertyName: "videos")]
    public Videos Videos { get; set; } = new();
}
