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
[Trait(name: "Category", value: "Unit")]
public class LogCategoriesTests
{
    [Theory]
    [InlineData(data: ["moviedb", "TheMovieDB"])]
    [InlineData(data: ["tvdb", "TheTVDB"])]
    [InlineData(data: ["musicbrainz", "MusicBrainz"])]
    [InlineData(data: ["MOVIEDB", "TheMovieDB"])]
    public void Resolve_ReturnsDisplayName(string key, string expected)
    {
        LogCategories.Resolve(key: key).DisplayName.Should().Be(expected: expected);
    }

    [Fact]
    public void Resolve_Unknown_FallsBackToDefault()
    {
        LogCategories.Resolve(key: "does-not-exist").Should().Be(expected: LogCategories.Default);
        LogCategories.Resolve(key: null).Should().Be(expected: LogCategories.Default);
    }

    [Theory]
    [InlineData(data: ["NoMercy.Providers.TMDB.Client.TmdbBaseClient", "moviedb"])]
    [InlineData(data: ["NoMercy.Providers.TVDB.Client.TvdbBaseClient", "tvdb"])]
    [InlineData(data: ["NoMercy.Encoder.Jobs.VideoEncodeJob", "encoder"])]
    [InlineData(data: ["NoMercyQueue.QueueWorker", "queue"])]
    [InlineData(data: ["NoMercy.Random.Unknown.Thing", "app"])]
    public void ResolveSource_MapsNamespaceToCategory(string source, string expectedKey)
    {
        LogCategories.ResolveSource(sourceContext: source).Key.Should().Be(expected: expectedKey);
    }

    [Fact]
    public void Providers_HaveDistinctDarkColours()
    {
        string[] providerKeys =
        {
            "youtube",
            "acoustid",
            "anidb",
            "audiodb",
            "coverart",
            "fanart",
            "fingerprint",
            "lrclib",
            "moviedb",
            "musicbrainz",
            "musixmatch",
            "opensubs",
            "tvdb",
        };

        IEnumerable<string> colours = providerKeys.Select(selector: k => LogCategories.Resolve(key: k).DarkHex);
        colours.Distinct().Should().HaveCount(expected: providerKeys.Length);
    }

    [Theory]
    [InlineData(data: "moviedb")]
    [InlineData(data: "info")]
    [InlineData(data: "queue")]
    public void Colours_AreValidHex(string key)
    {
        LogCategory category = LogCategories.Resolve(key: key);
        Regex hex = new(pattern: "^#[0-9a-f]{6}$");
        hex.IsMatch(input: category.DarkHex).Should().BeTrue();
        hex.IsMatch(input: category.LightHex).Should().BeTrue();
    }
}
