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
using NoMercy.MediaProcessing.Shows;
using NoMercy.Providers.Helpers;
using NoMercy.Tests.Common.Providers;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Shows;

/// <summary>
/// Kitsu's community catalogue lists non-Japanese productions that got a
/// fan-run entry — reproduced live: "Avatar: The Last Airbender" matches a real
/// Kitsu title by name alone. TvShowsController already guarded against this
/// with a Japanese-origin check, but that guard lived only there — every other
/// caller of the shared classifier (the anime/tv audit, new-show onboarding)
/// had no such protection and would misfile a Western co-production into the
/// anime library on title match alone.
/// </summary>
[Collection("HttpClientProvider")]
public sealed class MediaTypeClassifierTests : ProviderHttpHarness
{
    public MediaTypeClassifierTests()
        : base(HttpClientNames.KitsuIo) { }

    [Fact]
    public async Task ClassifyAsync_TitleMatchesButOriginIsNotJapan_ReturnsTv()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Avatar: The Last Airbender Book 1: Water"},"abbreviatedTitles":[]}}]}"""
            )
        );

        MediaTypeClassifier classifier = new();
        string? result = await classifier.ClassifyAsync("Avatar: The Last Airbender", 2005, ["US"]);

        result.Should().Be("tv");
    }

    [Fact]
    public async Task ClassifyAsync_TitleMatchesAndOriginIsJapan_ReturnsAnime()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Hunter x Hunter"},"abbreviatedTitles":[]}}]}"""
            )
        );

        MediaTypeClassifier classifier = new();
        string? result = await classifier.ClassifyAsync("Hunter x Hunter", 2011, ["JP"]);

        result.Should().Be("anime");
    }

    [Fact]
    public async Task ClassifyAsync_OriginUnknown_TrustsTheTitleMatch()
    {
        Handler.WhenGet(
            "anime",
            MockResponse.Json(
                HttpStatusCode.OK,
                """{"data":[{"attributes":{"titles":{"en":"Hunter x Hunter"},"abbreviatedTitles":[]}}]}"""
            )
        );

        MediaTypeClassifier classifier = new();
        string? result = await classifier.ClassifyAsync("Hunter x Hunter", 2011);

        result.Should().Be("anime");
    }
}
