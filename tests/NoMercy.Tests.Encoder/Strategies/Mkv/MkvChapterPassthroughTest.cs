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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Strategies.Mkv;

/// <summary>
/// Smoke test: MkvOutputStrategy must not emit <c>-map_metadata -1</c> (which
/// would strip the chapter track), and must emit a <c>-map</c> for the video
/// stream so FFmpeg naturally stream-copies chapter data from the source.
/// </summary>
public class MkvChapterPassthroughTest
{
    private static readonly IReadOnlyList<ChapterInfo> ThreeChapters =
    [
        new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 10), Title: "Opening"),
        new(Start: TimeSpan.FromMinutes(minutes: 10), End: TimeSpan.FromMinutes(minutes: 50), Title: "Act One"),
        new(Start: TimeSpan.FromMinutes(minutes: 50), End: TimeSpan.FromMinutes(minutes: 90), Title: "Finale"),
    ];

    [Fact]
    public void ConfigureOutput_WithChapters_DoesNotStripMapMetadata()
    {
        MkvOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlanWithChapters(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);

        // Must NOT contain -map_metadata -1 (that would strip chapters)
        args.Should().NotContain(unexpected: "-map_metadata -1");
    }

    [Fact]
    public void ConfigureOutput_WithChapters_ContainsVideoMapArg()
    {
        MkvOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlanWithChapters(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);

        // Must contain -map [v0] so the video stream (and implicitly chapters) is included
        args.Should().Contain(expected: "-map [v0]");
    }

    [Fact]
    public void ConfigureOutput_WithoutChapters_DoesNotStripMapMetadata()
    {
        MkvOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlanWithoutChapters(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);

        args.Should().NotContain(unexpected: "-map_metadata -1");
    }

    private static OutputPlan CreatePlanWithChapters() =>
        new(
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
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
            Chapters: ThreeChapters
        );

    private static OutputPlan CreatePlanWithoutChapters() =>
        new(
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
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
            Thumbnails: null
        );
}
