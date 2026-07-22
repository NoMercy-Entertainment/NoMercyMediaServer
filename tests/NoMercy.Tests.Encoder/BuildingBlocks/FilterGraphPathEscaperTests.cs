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

using NoMercy.Encoder.BuildingBlocks;

namespace NoMercy.Tests.Encoder.BuildingBlocks;

/// <summary>
/// FilterGraphPathEscaper is the single shared implementation for escaping a
/// path spliced into an FFmpeg filtergraph option value (lut3d=, ass=,
/// subtitles=). These tests assert the escaped output byte-for-byte so a
/// regression that stops escaping a metacharacter — or drops the quoting —
/// fails loudly. The quoting/escaping combination is verified against a real
/// ffmpeg build: quoting alone does not neutralise a drive-letter colon
/// (ffmpeg misreads the option boundary), and escaping without quoting
/// breaks graph-level comma/semicolon/bracket parsing. Both together are
/// required.
/// </summary>
public class FilterGraphPathEscaperTests
{
    [Fact]
    public void Escape_PlainUnixPath_WrapsInQuotesUnchanged()
    {
        string result = FilterGraphPathEscaper.Escape(path: "/usr/media/sub.ass");

        Assert.Equal(expected: "'/usr/media/sub.ass'", actual: result);
    }

    [Fact]
    public void Escape_WindowsDriveColon_EscapesColonAfterNormalisingSeparators()
    {
        string result = FilterGraphPathEscaper.Escape(path: @"C:\movies\file.ass");

        Assert.Equal(expected: "'C\\:/movies/file.ass'", actual: result);
    }

    [Fact]
    public void Escape_PathWithSingleQuote_EscapesQuote()
    {
        string result = FilterGraphPathEscaper.Escape(path: "/media/it's a show.ass");

        Assert.Equal(expected: "'/media/it\\'s a show.ass'", actual: result);
    }

    [Fact]
    public void Escape_PathWithBrackets_SurvivesUnescapedInsideQuotes()
    {
        string result = FilterGraphPathEscaper.Escape(path: "/media/[Group]release.ass");

        Assert.Equal(expected: "'/media/[Group]release.ass'", actual: result);
    }

    [Fact]
    public void Escape_PathWithCommaAndSemicolon_SurvivesUnescapedInsideQuotes()
    {
        string result = FilterGraphPathEscaper.Escape(path: "/media/a,b;c.ass");

        Assert.Equal(expected: "'/media/a,b;c.ass'", actual: result);
    }

    [Fact]
    public void Escape_PathWithSpaces_SurvivesUnescapedInsideQuotes()
    {
        string result = FilterGraphPathEscaper.Escape(path: "/movies/my film/subtitle.ass");

        Assert.Equal(expected: "'/movies/my film/subtitle.ass'", actual: result);
    }

    [Fact]
    public void Escape_CombinedSpecialCharacters_EscapesOnlyColonBackslashAndQuote()
    {
        string result = FilterGraphPathEscaper.Escape(path: @"C:\media\[grp] it's a, show;1.ass");

        Assert.Equal(expected: "'C\\:/media/[grp] it\\'s a, show;1.ass'", actual: result);
    }
}
