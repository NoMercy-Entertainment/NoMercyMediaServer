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

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbWatchProviderType
{
    [JsonProperty(propertyName: "link")]
    public Uri? Link { get; set; }

    [JsonProperty(propertyName: "buy")]
    public TmdbPaymentDetails[] Buy { get; set; } = [];

    [JsonProperty(propertyName: "flatrate")]
    public TmdbPaymentDetails[] FlatRate { get; set; } = [];

    [JsonProperty(propertyName: "ads")]
    public TmdbPaymentDetails[] Ads { get; set; } = [];

    [JsonProperty(propertyName: "rent")]
    public TmdbPaymentDetails[] Rent { get; set; } = [];

    [JsonProperty(propertyName: "free")]
    public TmdbPaymentDetails[] Free { get; set; } = [];
}
