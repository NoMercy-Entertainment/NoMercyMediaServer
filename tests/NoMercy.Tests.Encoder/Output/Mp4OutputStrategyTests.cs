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

public class Mp4OutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_HasFaststart()
    {
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-movflags +faststart");
    }

    [Fact]
    public void ConfigureOutput_ProducesMp4Output()
    {
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        cmd.Arguments.Should().Contain(predicate: a => a.Contains("output.mp4"));
    }

    [Fact]
    public void ConfigureOutput_PreserveDolbyVision_AddsDvh1Tag()
    {
        // DV passthrough in MP4 requires the dvh1 tag — without it Apple TV /
        // QuickTime drop the DV metadata and play as plain HDR10.
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with { PreserveDolbyVision = true };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-tag:v dvh1");
    }

    [Fact]
    public void ConfigureOutput_NoDolbyVision_OmitsDvh1Tag()
    {
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().NotContain(unexpected: "-tag:v dvh1");
    }

    [Fact]
    public void ConfigureOutput_AudioCopy_EmitsCopyCodecToken()
    {
        // Audio copy must use literal "copy" as the codec — not the source
        // encoder name. ffmpeg interprets "copy" as stream-copy.
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Copy, Language: "eng", MapLabel: "0:a:0")],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-c:a copy");
    }

    [Fact]
    public void ConfigureOutput_AudioFilter_AppliedWhenTranscoding()
    {
        // When the primary audio is transcoded AND carries a filter, the
        // strategy must emit -af with that filter. Required for downmix
        // (e.g. 5.1 → stereo) and loudness normalization.
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs =
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")
                {
                    AudioFilter = "loudnorm=I=-16:TP=-1.5",
                },
            ],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "loudnorm=I=-16:TP=-1.5");
    }

    [Fact]
    public void ConfigureOutput_AudioFilter_OmittedWhenCopy()
    {
        // Stream-copy can't take filters. Strategy must NOT emit -af.
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs =
            [
                new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Copy, Language: "eng", MapLabel: "0:a:0")
                {
                    AudioFilter = "loudnorm=I=-16",
                },
            ],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().NotContain(unexpected: "loudnorm=I=-16");
    }

    [Fact]
    public void ConfigureOutput_AudioWithDropAction_NotMapped()
    {
        // Drop = remove the stream entirely; it must not appear in -map.
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Drop, Language: "eng", MapLabel: "0:a:0")],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().NotContain(unexpected: "-map 0:a:0");
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsEmpty()
    {
        // MP4 is a single-file container — no subdirectories needed.
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());

        strategy.GetOutputSubdirectories(plan: CreatePlan()).Should().BeEmpty();
    }

    [Fact]
    public void Format_IsMp4()
    {
        new Mp4OutputStrategy(storage: TestStorageFactory.CreateLocal())
            .Format.Should()
            .Be(expected: OutputFormat.Mp4);
    }

    private static OutputPlan CreatePlan() =>
        new(
            Format: OutputFormat.Mp4,
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
