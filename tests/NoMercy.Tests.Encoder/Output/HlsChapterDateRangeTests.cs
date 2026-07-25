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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// Verifies that <see cref="PlaylistGenerator.GenerateMasterPlaylist"/> emits
/// <c>#EXT-X-DATERANGE</c> chapter tags (HLS v8) when the plan carries chapters,
/// and that none are emitted when chapters is null/empty.
/// </summary>
public class HlsChapterDateRangeTests
{
    private static readonly Dictionary<string, VariantMetrics> EmptyVideoMetrics = [];

    private static readonly Dictionary<string, VariantMetrics> EmptyAudioMetrics = [];

    private static readonly IReadOnlyList<ChapterInfo> ThreeChapters =
    [
        new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Opening"),
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), "Act One"),
        new(TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(90), "Finale"),
    ];

    private string Generate(OutputPlan plan)
    {
        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(
            plan,
            "Movie.Title",
            EmptyVideoMetrics,
            EmptyAudioMetrics
        );
    }

    // ------------------------------------------------------------------

    [Fact]
    public void WithChapters_EmitsOneDateRangeLinePerChapter()
    {
        string playlist = Generate(CreatePlan(ThreeChapters));

        int count = playlist
            .Split('\n')
            .Count(line => line.TrimEnd().StartsWith("#EXT-X-DATERANGE:"));

        count.Should().Be(ThreeChapters.Count);
    }

    [Fact]
    public void WithChapters_DateRangeIdFollowsChIndexFormat()
    {
        string playlist = Generate(CreatePlan(ThreeChapters));

        playlist.Should().Contain("ID=\"ch0\"");
        playlist.Should().Contain("ID=\"ch1\"");
        playlist.Should().Contain("ID=\"ch2\"");
    }

    [Fact]
    public void WithChapters_DurationIsEndMinusStartSeconds()
    {
        // Chapter 1: 0–600 s → 600.000 s
        // Chapter 2: 600–3000 s → 2400.000 s
        // Chapter 3: 3000–5400 s → 2400.000 s
        string playlist = Generate(CreatePlan(ThreeChapters));

        playlist.Should().Contain("DURATION=600.000");
        playlist.Should().Contain("DURATION=2400.000");
    }

    [Fact]
    public void WithChapters_StartDateIsUtcIso8601()
    {
        string playlist = Generate(CreatePlan(ThreeChapters));

        // Chapter 1 starts at epoch (0 s offset from Unix epoch)
        playlist.Should().Contain("START-DATE=\"1970-01-01T00:00:00.000Z\"");
        // Chapter 2 starts at 10 minutes = 600 s
        playlist.Should().Contain("START-DATE=\"1970-01-01T00:10:00.000Z\"");
    }

    [Fact]
    public void WithChapters_ChapterTitleAppearsInCustomAttribute()
    {
        string playlist = Generate(CreatePlan(ThreeChapters));

        playlist.Should().Contain("X-COM-NOMERCY-CHAPTER-TITLE=\"Opening\"");
        playlist.Should().Contain("X-COM-NOMERCY-CHAPTER-TITLE=\"Act One\"");
        playlist.Should().Contain("X-COM-NOMERCY-CHAPTER-TITLE=\"Finale\"");
    }

    [Fact]
    public void WithChapters_VersionBumpedToV8()
    {
        string playlist = Generate(CreatePlan(ThreeChapters));

        playlist.Should().Contain("#EXT-X-VERSION:8");
    }

    [Fact]
    public void WithoutChapters_NoDatRangeLines()
    {
        string playlist = Generate(CreatePlan(null));

        playlist.Should().NotContain("#EXT-X-DATERANGE:");
    }

    [Fact]
    public void WithEmptyChapterList_NoDatRangeLines()
    {
        string playlist = Generate(CreatePlan([]));

        playlist.Should().NotContain("#EXT-X-DATERANGE:");
    }

    [Fact]
    public void WithChapters_TitleWithQuotesIsEscaped()
    {
        IReadOnlyList<ChapterInfo> chapters =
        [
            new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "It's \"Alive\""),
        ];
        string playlist = Generate(CreatePlan(chapters));

        // Double quotes inside title must be escaped as \"
        playlist.Should().Contain("X-COM-NOMERCY-CHAPTER-TITLE=\"It's \\\"Alive\\\"\"");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static OutputPlan CreatePlan(IReadOnlyList<ChapterInfo>? chapters) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    8000,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: chapters
        );
}
