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

using System.Text.RegularExpressions;
using NoMercy.NmSystem.Logging;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins <see cref="LogCategories"/>: keys resolve to their display name, every
/// provider gets a distinct legible colour, source contexts map to the right
/// category, and unknown input falls back to the default.
/// </summary>
[Trait("Category", "Unit")]
public class LogCategoriesTests
{
    [Theory]
    [InlineData("moviedb", "TheMovieDB")]
    [InlineData("tvdb", "TheTVDB")]
    [InlineData("musicbrainz", "MusicBrainz")]
    [InlineData("MOVIEDB", "TheMovieDB")]
    public void Resolve_ReturnsDisplayName(string key, string expected)
    {
        LogCategories.Resolve(key).DisplayName.Should().Be(expected);
    }

    [Fact]
    public void Resolve_Unknown_FallsBackToDefault()
    {
        LogCategories.Resolve("does-not-exist").Should().Be(LogCategories.Default);
        LogCategories.Resolve(null).Should().Be(LogCategories.Default);
    }

    [Theory]
    [InlineData("NoMercy.Providers.TMDB.Client.TmdbBaseClient", "moviedb")]
    [InlineData("NoMercy.Providers.TVDB.Client.TvdbBaseClient", "tvdb")]
    [InlineData("NoMercy.Encoder.Jobs.VideoEncodeJob", "encoder")]
    [InlineData("NoMercyQueue.QueueWorker", "queue")]
    [InlineData("NoMercy.Random.Unknown.Thing", "app")]
    public void ResolveSource_MapsNamespaceToCategory(string source, string expectedKey)
    {
        LogCategories.ResolveSource(source).Key.Should().Be(expectedKey);
    }

    [Fact]
    public void Providers_HaveDistinctDarkColours()
    {
        string[] providerKeys =
        {
            "youtube", "acoustid", "anidb", "audiodb", "coverart", "fanart",
            "fingerprint", "lrclib", "moviedb", "musicbrainz", "musixmatch",
            "opensubs", "tvdb",
        };

        IEnumerable<string> colours = providerKeys.Select(k => LogCategories.Resolve(k).DarkHex);
        colours.Distinct().Should().HaveCount(providerKeys.Length);
    }

    [Theory]
    [InlineData("moviedb")]
    [InlineData("info")]
    [InlineData("queue")]
    public void Colours_AreValidHex(string key)
    {
        LogCategory category = LogCategories.Resolve(key);
        Regex hex = new("^#[0-9a-f]{6}$");
        hex.IsMatch(category.DarkHex).Should().BeTrue();
        hex.IsMatch(category.LightHex).Should().BeTrue();
    }
}
