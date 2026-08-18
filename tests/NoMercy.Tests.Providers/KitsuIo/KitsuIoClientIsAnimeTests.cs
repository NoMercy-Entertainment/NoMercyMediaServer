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

using System.Net;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.KitsuIo;
using NoMercy.Tests.Common.Providers;
using Xunit;

namespace NoMercy.Tests.Providers.KitsuIo;

/// <summary>
/// Two independent bugs conspired to misfile genuinely-anime shows (Hunter x
/// Hunter, Fruits Basket, Little Witch Academia — all reproduced live):
/// <para>
/// 1. A sequential audit over hundreds of shows trips Kitsu's rate limit partway
/// through, and a non-2xx response used to deserialize into an empty candidate
/// list — collapsing "the lookup failed" into "confirmed not anime" with no
/// distinction. IsAnime must return null for a failed lookup.
/// </para>
/// <para>
/// 2. Even a successful lookup could still fail an exact string match: Kitsu's
/// canonical title often carries a "(YYYY)" disambiguation suffix the local
/// title lacks, uses typographic punctuation where the local title has a plain
/// one, and the "en_us" field — sometimes the only populated title — was never
/// checked at all.
/// </para>
/// </summary>
[Collection("HttpClientProvider")]
public sealed class KitsuIoClientIsAnimeTests : ProviderHttpHarness
{
    public KitsuIoClientIsAnimeTests()
        : base(HttpClientNames.KitsuIo) { }

    [Fact]
    public async Task IsAnime_MatchingTitle_ReturnsTrue()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Hunter x Hunter"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime("Hunter x Hunter", 2011);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Kitsu's canonical en/en_jp title commonly carries a disambiguating year
    /// suffix the local title never has — reproduced live: Kitsu answers "Fruits
    /// Basket" with en: "Fruits Basket (2019)", and the old exact-Equals match
    /// failed it, reporting a genuine anime as "tv".
    /// </summary>
    [Fact]
    public async Task IsAnime_CandidateTitleHasYearSuffix_StillMatches()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Fruits Basket (2019)"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime("Fruits Basket", 2019);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Kitsu's editors use typographic punctuation (a curly apostrophe) where the
    /// local title has a plain one — reproduced live against Frieren's actual
    /// Kitsu entry.
    /// </summary>
    [Fact]
    public async Task IsAnime_CandidateTitleUsesCurlyApostrophe_StillMatches()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Frieren: Beyond Journey’s End"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime("Frieren: Beyond Journey's End", 2023);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Reproduced live: Little Witch Academia's Kitsu entry has no "en" title at
    /// all, only "en_us" — a field the matcher never checked.
    /// </summary>
    [Fact]
    public async Task IsAnime_OnlyEnUsTitlePopulated_StillMatches()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en_us":"Little Witch Academia"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime("Little Witch Academia", 2017);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Reproduced live in both directions: Kitsu's canonical title sets off a
    /// subtitle with a dash where the local title uses a colon ("Nichijou: My
    /// Ordinary Life" locally vs Kitsu's "Nichijou - My Ordinary Life"), and vice
    /// versa for other shows ("KONOSUBA - God's blessing..." locally vs Kitsu's
    /// "KonoSuba: God's Blessing..."). Sanitize() alone keeps "-", so an
    /// unnormalized comparison fails both directions.
    /// </summary>
    [Fact]
    public async Task IsAnime_ColonVsDashSubtitleSeparator_StillMatches()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Nichijou - My Ordinary Life"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime("Nichijou: My Ordinary Life", 2011);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Reproduced live: Kitsu's own full-text search reads a leading "-" in
    /// filter[text] as an exclusion token — querying the raw local title
    /// "Re:ZERO -Starting Life in Another World-" returns zero candidates, while
    /// the same query with the dash-delimited subtitle stripped ("Re:ZERO")
    /// finds the show immediately. The query sent to Kitsu, not just the
    /// candidate comparison, must have its dashes normalized away first.
    /// </summary>
    [Fact]
    public async Task IsAnime_QueryWithLeadingDashSubtitle_SearchesWithoutTheDash()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Re:ZERO -Starting Life in Another World-"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime(
            "Re:ZERO -Starting Life in Another World-",
            2016
        );

        result.Should().BeTrue();
    }

    /// <summary>
    /// Reproduced live: Kitsu answers "SAINT SEIYA: Knights of the Zodiac" with
    /// en "Knights of the Zodiac: Saint Seiya" — the exact same words, reordered.
    /// A prefix/substring compare can never match a reordering; only a word-set
    /// comparison can.
    /// </summary>
    [Fact]
    public async Task IsAnime_CandidateWordsReordered_StillMatches()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Knights of the Zodiac: Saint Seiya"},"abbreviatedTitles":[]}}]}"""
            )
        );

        bool? result = await KitsuIoClient.IsAnime("SAINT SEIYA: Knights of the Zodiac", 2019);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAnime_NoMatchingTitle_ReturnsFalse()
    {
        Handler.WhenGet("anime", MockResponse.Json(HttpStatusCode.OK, """{"data":[]}"""));

        bool? result = await KitsuIoClient.IsAnime("Extras", 2005);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAnime_RateLimited_ReturnsNullNotFalse()
    {
        Handler.WhenGet("anime", MockResponse.Status(HttpStatusCode.TooManyRequests));

        bool? result = await KitsuIoClient.IsAnime("Hunter x Hunter", 2011);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsAnime_ServiceUnavailable_ReturnsNullNotFalse()
    {
        Handler.WhenGet("anime", MockResponse.Status(HttpStatusCode.ServiceUnavailable));

        bool? result = await KitsuIoClient.IsAnime("Fruits Basket", 2019);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsAnime_MalformedResponseBody_ReturnsNullNotFalse()
    {
        Handler.WhenGet("anime", MockResponse.Malformed());

        bool? result = await KitsuIoClient.IsAnime("Little Witch Academia", 2017);

        result.Should().BeNull();
    }
}
