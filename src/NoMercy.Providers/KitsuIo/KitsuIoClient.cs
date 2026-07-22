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

using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.KitsuIo;

public static class KitsuIoClient
{
    public static async Task<bool> IsAnime(string title, int year)
    {
        bool isAnime = false;

        HttpClient client = HttpClientProvider.CreateClient(name: HttpClientNames.KitsuIo);

        using HttpResponseMessage response = await client.GetAsync(
            requestUri: $"anime?filter[text]={title}&filter[year]={year}"
        );
        string content = await response.Content.ReadAsStringAsync();

        try
        {
            KitsuAnime? anime = content.FromJson<KitsuAnime>();

            foreach (Data data in anime?.Data ?? [])
                if (
                    data.Attributes.Titles.En?.Equals(
                        value: title,
                        comparisonType: StringComparison.CurrentCultureIgnoreCase
                    ) == true
                )
                    isAnime = true;
                else if (
                    data.Attributes.Titles.EnJp?.Equals(
                        value: title,
                        comparisonType: StringComparison.CurrentCultureIgnoreCase
                    ) == true
                )
                    isAnime = true;
                else if (
                    data.Attributes.Titles.JaJp?.Equals(
                        value: title,
                        comparisonType: StringComparison.CurrentCultureIgnoreCase
                    ) == true
                )
                    isAnime = true;
                else if (
                    data.Attributes.Titles.ThTh?.Equals(
                        value: title,
                        comparisonType: StringComparison.CurrentCultureIgnoreCase
                    ) == true
                )
                    isAnime = true;
                else if (
                    data.Attributes.AbbreviatedTitles.Any(predicate: abbreviatedTitle =>
                        abbreviatedTitle.Equals(value: title, comparisonType: StringComparison.CurrentCultureIgnoreCase)
                    )
                )
                    isAnime = true;
        }
        catch (Exception e)
        {
            Logger.AniDb(message: e.Message, level: LogEventLevel.Fatal);
        }

        return isAnime;
    }
}
