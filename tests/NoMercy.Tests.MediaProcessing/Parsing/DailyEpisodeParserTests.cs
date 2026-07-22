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
using NoMercy.MediaProcessing.Files.Parsing;

namespace NoMercy.Tests.MediaProcessing.Parsing;

/// <summary>
/// Corpus for <see cref="DailyEpisodeParser.TryGetAirDate"/>: dated daily-show
/// files resolve to the air date; bare years, resolutions and invalid dates do not.
/// </summary>
public class DailyEpisodeParserTests
{
    [Theory]
    [InlineData(data: ["The.Daily.Show.2024.01.15.1080p.WEB.x264-GROUP.mkv", 2024, 1, 15])]
    [InlineData(data: ["Conan.2015.02.03.x264.mkv", 2015, 2, 3])]
    [InlineData(data: ["Jimmy.Kimmel.Live.2023.12.31.720p.HDTV.mkv", 2023, 12, 31])]
    [InlineData(data: ["Show 2024-06-09 1080p.mkv", 2024, 6, 9])]
    [InlineData(data: ["Late.Night.1999.09.09.mkv", 1999, 9, 9])]
    public void Parses_air_date(string name, int y, int m, int d)
    {
        DateOnly? date = DailyEpisodeParser.TryGetAirDate(name: name);
        date.Should().Be(expected: new(year: y, month: m, day: d));
    }

    [Theory]
    [InlineData(data: "Movie.2024.1080p.BluRay.x264.mkv")] // year only
    [InlineData(data: "Show.S01E05.2024.mkv")] // SxxExx + bare year
    [InlineData(data: "Film.1920x1080.mkv")] // resolution
    [InlineData(data: "Show.2024.13.45.mkv")] // invalid month/day
    [InlineData(data: "Show.2024.00.10.mkv")] // month 00
    [InlineData(data: "Apollo.13.mkv")] // unrelated number
    [InlineData(data: "")]
    public void Rejects_non_dates(string name) =>
        DailyEpisodeParser.TryGetAirDate(name: name).Should().BeNull();
}
