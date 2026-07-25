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

public class TmdbPersonAppends : TmdbPersonDetails
{
    [JsonProperty("movie_credits")]
    public TmdbPersonCredits MovieCredits { get; set; } = new();

    [JsonProperty("credits")]
    public TmdbPersonCredits Credits { get; set; } = new();

    [JsonProperty("combined_credits")]
    public TmdbPersonCredits CombinedCredits { get; set; } = new();

    [JsonProperty("tv_credits")]
    public TmdbPersonCredits TvCredits { get; set; } = new();

    [JsonProperty("images")]
    public TmdbPersonImages Images { get; set; } = new();

    [JsonProperty("translations")]
    public TmdbPersonTranslations Translations { get; set; } = new();
}
