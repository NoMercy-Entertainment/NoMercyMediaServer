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
/// A sequential audit over hundreds of shows trips Kitsu's rate limit partway
/// through. Before this, a non-2xx response body failed to deserialize into the
/// expected shape, <c>Data</c> came back empty, and the loop that looks for a
/// title match simply never ran — collapsing "the lookup failed" into "confirmed
/// not anime" with no distinction. That silently moved genuinely-anime shows
/// (Hunter x Hunter, Fruits Basket, Little Witch Academia — all reproduced live)
/// out of the library on nothing more than a rate-limited response. IsAnime must
/// return null for a failed lookup, never collapse it into false.
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
