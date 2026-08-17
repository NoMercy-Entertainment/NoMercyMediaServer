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

using NoMercy.Providers.KitsuIo;
using Xunit;

namespace NoMercy.Tests.Providers.KitsuIo;

/// <summary>
/// <c>filter[text]</c> used to carry the show's title unescaped into the query
/// string. Any title with a space — almost every real title — produced a
/// malformed request that Kitsu answered with zero candidates, so the anime/tv
/// classifier silently voted "not anime" for shows it never actually looked up.
/// Reproduced live: "Hunter x Hunter" raw returns nothing from kitsu.io;
/// URL-encoded, it resolves on the first result.
/// </summary>
[Trait("Category", "Unit")]
public sealed class KitsuIoClientQueryTests
{
    [Fact]
    public void BuildQuery_TitleWithSpaces_EncodesEachSpace()
    {
        string query = KitsuIoClient.BuildQuery("Hunter x Hunter", 2011);

        Assert.DoesNotContain(" ", query);
        Assert.Contains("filter[text]=Hunter%20x%20Hunter", query);
        Assert.Contains("filter[year]=2011", query);
    }

    [Fact]
    public void BuildQuery_TitleWithReservedUriCharacters_DoesNotFragmentTheQuery()
    {
        // "&" would otherwise be read as introducing filter[year] mid-title;
        // "#" would otherwise be read as starting a URI fragment.
        string query = KitsuIoClient.BuildQuery(
            "Fate/stay night: Heaven's Feel & #compilation",
            2017
        );

        Assert.Equal(1, CountOccurrences(query, "filter[year]="));
        Assert.DoesNotContain("#", query);
    }

    [Fact]
    public void BuildQuery_PlainAsciiTitle_RoundTripsUnchanged()
    {
        string query = KitsuIoClient.BuildQuery("Castlevania", 2017);

        Assert.Equal("anime?filter[text]=Castlevania&filter[year]=2017", query);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
