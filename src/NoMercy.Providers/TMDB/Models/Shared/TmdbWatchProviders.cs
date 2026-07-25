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

public class TmdbWatchProviders
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("results")]
    public TmdbWatchProviderResults TmdbWatchProviderResults { get; set; } = new();

    public static IEnumerable<(
        string CountryCode,
        string ProviderType,
        TmdbPaymentDetails Provider,
        string? Link
    )> ExtractProviders(TmdbWatchProviderResults results)
    {
        foreach ((string key, TmdbWatchProviderType value) in results)
        {
            string countryCode = key.ToUpper();
            string? link = value.Link?.ToString();

            Dictionary<string, TmdbPaymentDetails[]?> providerTypeMap = new()
            {
                ["flatrate"] = value.FlatRate,
                ["buy"] = value.Buy,
                ["rent"] = value.Rent,
                ["ads"] = value.Ads,
                ["free"] = value.Free,
            };

            foreach ((string providerType, TmdbPaymentDetails[]? providers) in providerTypeMap)
            {
                if (providers == null)
                    continue;

                foreach (TmdbPaymentDetails provider in providers)
                {
                    yield return (countryCode, providerType, provider, link);
                }
            }
        }
    }
}
