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
        new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 10), Title: "Opening"),
        new(Start: TimeSpan.FromMinutes(minutes: 10), End: TimeSpan.FromMinutes(minutes: 50), Title: "Act One"),
        new(Start: TimeSpan.FromMinutes(minutes: 50), End: TimeSpan.FromMinutes(minutes: 90), Title: "Finale"),
    ];

    private string Generate(OutputPlan plan)
    {
        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(
            plan: plan,
            mediaTitle: "Movie.Title",
            videoMetrics: EmptyVideoMetrics,
            audioMetrics: EmptyAudioMetrics
        );
    }

    // ------------------------------------------------------------------

    [Fact]
    public void WithChapters_EmitsOneDateRangeLinePerChapter()
    {
        string playlist = Generate(plan: CreatePlan(chapters: ThreeChapters));

        int count = playlist
            .Split(separator: '\n')
            .Count(predicate: line => line.TrimEnd().StartsWith(value: "#EXT-X-DATERANGE:"));

        count.Should().Be(expected: ThreeChapters.Count);
    }

    [Fact]
    public void WithChapters_DateRangeIdFollowsChIndexFormat()
    {
        string playlist = Generate(plan: CreatePlan(chapters: ThreeChapters));

        playlist.Should().Contain(expected: "ID=\"ch0\"");
        playlist.Should().Contain(expected: "ID=\"ch1\"");
        playlist.Should().Contain(expected: "ID=\"ch2\"");
    }

    [Fact]
    public void WithChapters_DurationIsEndMinusStartSeconds()
    {
        // Chapter 1: 0–600 s → 600.000 s
        // Chapter 2: 600–3000 s → 2400.000 s
        // Chapter 3: 3000–5400 s → 2400.000 s
        string playlist = Generate(plan: CreatePlan(chapters: ThreeChapters));

        playlist.Should().Contain(expected: "DURATION=600.000");
        playlist.Should().Contain(expected: "DURATION=2400.000");
    }

    [Fact]
    public void WithChapters_StartDateIsUtcIso8601()
    {
        string playlist = Generate(plan: CreatePlan(chapters: ThreeChapters));

        // Chapter 1 starts at epoch (0 s offset from Unix epoch)
        playlist.Should().Contain(expected: "START-DATE=\"1970-01-01T00:00:00.000Z\"");
        // Chapter 2 starts at 10 minutes = 600 s
        playlist.Should().Contain(expected: "START-DATE=\"1970-01-01T00:10:00.000Z\"");
    }

    [Fact]
    public void WithChapters_ChapterTitleAppearsInCustomAttribute()
    {
        string playlist = Generate(plan: CreatePlan(chapters: ThreeChapters));

        playlist.Should().Contain(expected: "X-COM-NOMERCY-CHAPTER-TITLE=\"Opening\"");
        playlist.Should().Contain(expected: "X-COM-NOMERCY-CHAPTER-TITLE=\"Act One\"");
        playlist.Should().Contain(expected: "X-COM-NOMERCY-CHAPTER-TITLE=\"Finale\"");
    }

    [Fact]
    public void WithChapters_VersionBumpedToV8()
    {
        string playlist = Generate(plan: CreatePlan(chapters: ThreeChapters));

        playlist.Should().Contain(expected: "#EXT-X-VERSION:8");
    }

    [Fact]
    public void WithoutChapters_NoDatRangeLines()
    {
        string playlist = Generate(plan: CreatePlan(chapters: null));

        playlist.Should().NotContain(unexpected: "#EXT-X-DATERANGE:");
    }

    [Fact]
    public void WithEmptyChapterList_NoDatRangeLines()
    {
        string playlist = Generate(plan: CreatePlan(chapters: []));

        playlist.Should().NotContain(unexpected: "#EXT-X-DATERANGE:");
    }

    [Fact]
    public void WithChapters_TitleWithQuotesIsEscaped()
    {
        IReadOnlyList<ChapterInfo> chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 10), Title: "It's \"Alive\""),
        ];
        string playlist = Generate(plan: CreatePlan(chapters: chapters));

        // Double quotes inside title must be escaped as \"
        playlist.Should().Contain(expected: "X-COM-NOMERCY-CHAPTER-TITLE=\"It's \\\"Alive\\\"\"");
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
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 8000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: chapters
        );
}
