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
/// Corpus for <see cref="EpisodeRangeParser.Expand"/>: joined multi-episode files
/// expand to every episode they cover, single episodes stay single, and bare
/// resolution/codec numbers never leak into the range.
/// </summary>
public class EpisodeRangeParserTests
{
    [Theory]
    // SxxExx repeats and ranges
    [InlineData("Show.S01E01.1080p.WEB-DL.x265.mkv", 1, 1, new[] { 1 })]
    [InlineData("Show.S01E01E02.1080p.mkv", 1, 1, new[] { 1, 2 })]
    [InlineData("Show.S01E01E02E03.mkv", 1, 1, new[] { 1, 2, 3 })]
    [InlineData("Show.S01E01-E03.mkv", 1, 1, new[] { 1, 2, 3 })]
    [InlineData("Show.S01E01-03.mkv", 1, 1, new[] { 1, 2, 3 })]
    [InlineData("Show.S01E01-E02.mkv", 1, 1, new[] { 1, 2 })]
    [InlineData("Show.S01E01 - E04.mkv", 1, 1, new[] { 1, 2, 3, 4 })]
    [InlineData("S01E01E02.mkv", 1, 1, new[] { 1, 2 })]
    [InlineData("One.Piece.S01E1109.mkv", 1, 1109, new[] { 1109 })]
    // cross-format
    [InlineData("Show.1x01.mkv", 1, 1, new[] { 1 })]
    [InlineData("Show.1x01-1x03.mkv", 1, 1, new[] { 1, 2, 3 })]
    [InlineData("Show.1x01-03.mkv", 1, 1, new[] { 1, 2, 3 })]
    [InlineData("Show.1x01x02.mkv", 1, 1, new[] { 1, 2 })]
    // guards: resolution/codec digits and runaway ranges never expand
    [InlineData("Show.S01E05.1080p.x265.mkv", 1, 5, new[] { 5 })]
    [InlineData("Show.S01E01-1080.mkv", 1, 1, new[] { 1 })]
    [InlineData("Show.S01E01.720p.mkv", 1, 1, new[] { 1 })]
    public void Expands(string name, int season, int first, int[] expected) =>
        EpisodeRangeParser.Expand(name, season, first).Should().Equal(expected);

    [Fact]
    public void Mismatched_anchor_returns_single()
    {
        // file is S02E05 but caller asks about S01E01 -> no expansion
        EpisodeRangeParser.Expand("Show.S02E05.mkv", 1, 1).Should().Equal(1);
    }
}
