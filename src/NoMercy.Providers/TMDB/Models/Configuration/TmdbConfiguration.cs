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

namespace NoMercy.Providers.TMDB.Models.Configuration;

public class TmdbConfiguration
{
    [JsonProperty("images")]
    public TmdbImage TmdbImages { get; set; } = new();

    [JsonProperty("change_keys")]
    public string[] ChangeKeys { get; set; } = [];

    public class TmdbImage
    {
        [JsonProperty("base_url")]
        public string BaseUrl { get; set; } = string.Empty;

        [JsonProperty("secure_base_url")]
        public string SecureBaseUrl { get; set; } = string.Empty;

        [JsonProperty("backdrop_sizes")]
        public string[] BackdropSizes { get; set; } = [];

        [JsonProperty("logo_sizes")]
        public string[] LogoSizes { get; set; } = [];

        [JsonProperty("poster_sizes")]
        public string[] PosterSizes { get; set; } = [];

        [JsonProperty("profile_sizes")]
        public string[] ProfileSizes { get; set; } = [];

        [JsonProperty("still_sizes")]
        public string[] StillSizes { get; set; } = [];
    }
}
