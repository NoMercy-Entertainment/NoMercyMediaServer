// -------- -----------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -------- -----------------------------------------------------------------------

using System.Globalization;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Output;

public class PlaylistGeneratorChapterTests
{
    private const string MediaTitle = "Test.Title";

    private string GenerateWithChapters(OutputPlan plan)
    {
        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: v => VideoVariantKey(video: v),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );

        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            keySelector: a => AudioVariantKey(audio: a),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
        );

        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);
    }

    private static string VideoVariantKey(VideoOutputPlan video) =>
        TemplateResolver.Resolve(
            template: video.PlaylistNameTemplate,
            values: TemplateResolver.VideoTokens(width: video.Width, height: video.Height, isHdrOutput: video.IsHdrOutput)
        );

    private static string AudioVariantKey(AudioOutputPlan audio) =>
        TemplateResolver.Resolve(
            template: audio.PlaylistNameTemplate,
            values: TemplateResolver.AudioTokens(language: audio.Language ?? "und", codecName: audio.CodecToken, channels: audio.Channels)
        );

    [Fact]
    public void GenerateMasterPlaylist_NoChapters_OmitsDaterRanges()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: null
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should().NotContain(unexpected: "#EXT-X-DATERANGE");
    }

    [Fact]
    public void GenerateMasterPlaylist_WithChapters_EmitsDaterRanges()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Chapter 1"),
                new(Start: TimeSpan.FromMinutes(minutes: 5), End: TimeSpan.FromMinutes(minutes: 15), Title: "Chapter 2"),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should().Contain(expected: "#EXT-X-DATERANGE");
        master.Should().Contain(expected: "ID=\"ch0\"");
        master.Should().Contain(expected: "ID=\"ch1\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_ChapterDates_ArIso8601()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.Zero, End: TimeSpan.FromSeconds(seconds: 60), Title: "Intro"),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should().Contain(expected: "START-DATE=\"1970-01-01T00:00:00.000Z\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_ChapterDuration_Correct()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.FromSeconds(seconds: 30), End: TimeSpan.FromSeconds(seconds: 90), Title: "Act 1"),
                new(Start: TimeSpan.FromSeconds(seconds: 90), End: TimeSpan.FromSeconds(seconds: 150), Title: "Act 2"),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should().Contain(expected: "DURATION=60");
    }

    [Fact]
    public void GenerateMasterPlaylist_ChapterTitle_EscapesQuotes()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Chapter \"1\" with \"quotes\""),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should()
            .Contain(expected: "X-COM-NOMERCY-CHAPTER-TITLE=\"Chapter \\\"1\\\" with \\\"quotes\\\"\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_MultipleChapters_SequentialIds()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.FromMinutes(minutes: 0), End: TimeSpan.FromMinutes(minutes: 10), Title: "Chapter 1"),
                new(Start: TimeSpan.FromMinutes(minutes: 10), End: TimeSpan.FromMinutes(minutes: 20), Title: "Chapter 2"),
                new(Start: TimeSpan.FromMinutes(minutes: 20), End: TimeSpan.FromMinutes(minutes: 30), Title: "Chapter 3"),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        int chapterCount = master.Split(separator: "ID=\"ch").Length - 1;
        chapterCount.Should().Be(expected: 3);

        master.Should().Contain(expected: "ID=\"ch0\"");
        master.Should().Contain(expected: "ID=\"ch1\"");
        master.Should().Contain(expected: "ID=\"ch2\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_EmptyChapterTitle_GeneratesDefault()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.FromSeconds(seconds: 0), End: TimeSpan.FromSeconds(seconds: 60), Title: null),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should().Contain(expected: "X-COM-NOMERCY-CHAPTER-TITLE=\"Chapter 1\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_ChaptersWithoutChaptersOption_Ignored()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: []
        );

        string master = GenerateWithChapters(plan: plan);

        master.Should().NotContain(unexpected: "#EXT-X-DATERANGE");
    }

    [Fact]
    public void GenerateMasterPlaylist_ChapterDurationUsingNextChapterStart()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(Start: TimeSpan.FromSeconds(seconds: 0), End: TimeSpan.FromSeconds(seconds: 1000), Title: "Chapter 1"),
                new(Start: TimeSpan.FromSeconds(seconds: 30), End: TimeSpan.FromSeconds(seconds: 1000), Title: "Chapter 2"),
            ]
        );

        string master = GenerateWithChapters(plan: plan);

        string[] dateRangeLines = master.Split(separator: '\n').Where(predicate: l => l.Contains(value: "ID=\"ch")).ToArray();
        dateRangeLines.Should().HaveCount(expected: 2);

        dateRangeLines[0].Should().Contain(expected: "DURATION=30");
        dateRangeLines[1].Should().Contain(expected: "DURATION=970");
    }
}
