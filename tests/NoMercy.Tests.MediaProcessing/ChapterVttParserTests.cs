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

using FluentAssertions;
using NoMercy.Database.Models.Media;
using NoMercy.MediaProcessing.Files;
using Xunit;

namespace NoMercy.Tests.MediaProcessing;

[Trait("Category", "Unit")]
public class ChapterVttParserTests
{
    // The exact shape ChapterWriter produces: WEBVTT header, then blank-line
    // separated blocks of `Chapter N` / timing / title.
    private const string RealChaptersVtt =
        "WEBVTT\n\n"
        + "Chapter 1\n00:00:00.000 --> 00:01:14.833\nScene 01\n\n"
        + "Chapter 2\n00:01:14.833 --> 00:02:17.417\nIntro\n\n"
        + "Chapter 3\n00:02:17.417 --> 00:26:32.417\nScene 03\n\n"
        + "Chapter 4\n00:26:32.417 --> 00:28:46.084\nCredits\n";

    [Fact]
    public void Parses_real_chapter_vtt_into_titled_cues_in_milliseconds()
    {
        List<IChapter> chapters = FileManager.ParseChaptersVtt(RealChaptersVtt);

        chapters.Should().HaveCount(4);

        chapters[0]
            .Should()
            .BeEquivalentTo(
                new
                {
                    Id = 0,
                    StartTime = 0,
                    EndTime = 74833,
                    Title = "Scene 01",
                }
            );
        chapters[1]
            .Should()
            .BeEquivalentTo(
                new
                {
                    Id = 1,
                    StartTime = 74833,
                    EndTime = 137417,
                    Title = "Intro",
                }
            );
        chapters[2]
            .Should()
            .BeEquivalentTo(
                new
                {
                    Id = 2,
                    StartTime = 137417,
                    EndTime = 1592417,
                    Title = "Scene 03",
                }
            );
        chapters[3]
            .Should()
            .BeEquivalentTo(
                new
                {
                    Id = 3,
                    StartTime = 1592417,
                    EndTime = 1726084,
                    Title = "Credits",
                }
            );
    }

    [Fact]
    public void Does_not_emit_header_blank_or_id_lines_as_chapters()
    {
        List<IChapter> chapters = FileManager.ParseChaptersVtt(RealChaptersVtt);

        chapters
            .Select(chapter => chapter.Title)
            .Should()
            .NotContain(["WEBVTT", "", "Chapter 1", "00:00:00.000 --> 00:01:14.833"]);
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        string crlf = RealChaptersVtt.Replace("\n", "\r\n");

        List<IChapter> chapters = FileManager.ParseChaptersVtt(crlf);

        chapters.Should().HaveCount(4);
        chapters[0].Title.Should().Be("Scene 01");
        chapters[0].StartTime.Should().Be(0);
    }

    [Fact]
    public void Skips_cues_with_unparseable_timing()
    {
        const string malformed =
            "WEBVTT\n\n"
            + "Chapter 1\nnot-a-timestamp --> also-bad\nBroken\n\n"
            + "Chapter 2\n00:00:10.000 --> 00:00:20.000\nGood\n";

        List<IChapter> chapters = FileManager.ParseChaptersVtt(malformed);

        chapters.Should().ContainSingle();
        chapters[0].Title.Should().Be("Good");
        chapters[0].StartTime.Should().Be(10000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("WEBVTT")]
    [InlineData("WEBVTT\n\n")]
    public void Returns_empty_for_headerless_or_cueless_input(string text)
    {
        FileManager.ParseChaptersVtt(text).Should().BeEmpty();
    }
}
