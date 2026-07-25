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
        new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Opening"),
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), "Act One"),
        new(TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(90), "Finale"),
    ];

    [Fact]
    public void ConfigureOutput_WithChapters_DoesNotStripMapMetadata()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlanWithChapters(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        // Must NOT contain -map_metadata -1 (that would strip chapters)
        args.Should().NotContain("-map_metadata -1");
    }

    [Fact]
    public void ConfigureOutput_WithChapters_ContainsVideoMapArg()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlanWithChapters(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        // Must contain -map [v0] so the video stream (and implicitly chapters) is included
        args.Should().Contain("-map [v0]");
    }

    [Fact]
    public void ConfigureOutput_WithoutChapters_DoesNotStripMapMetadata()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlanWithoutChapters(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        args.Should().NotContain("-map_metadata -1");
    }

    private static OutputPlan CreatePlanWithChapters() =>
        new(
            OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    0,
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
            Chapters: ThreeChapters
        );

    private static OutputPlan CreatePlanWithoutChapters() =>
        new(
            OutputFormat.Mkv,
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    0,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            [],
            null
        );
}
