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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class MkvOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_ProducesOutputMkv()
    {
        MkvOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreateSimplePlan(format: OutputFormat.Mkv), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        cmd.Arguments.Should().Contain(predicate: a => a.Contains("output.mkv"));
    }

    [Fact]
    public void ConfigureOutput_MapsAllStreams()
    {
        MkvOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreateSimplePlan(format: OutputFormat.Mkv), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-map [v0]");
        args.Should().Contain(expected: "-map 0:a:0");
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsEmpty()
    {
        MkvOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        strategy.GetOutputSubdirectories(plan: CreateSimplePlan(format: OutputFormat.Mkv)).Should().BeEmpty();
    }

    private static OutputPlan CreateSimplePlan(OutputFormat format) =>
        new(
            Format: format,
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
