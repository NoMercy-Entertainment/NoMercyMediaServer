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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.EdgeCases;

/// <summary>
/// Guards constant-frame-rate normalization for variable-frame-rate sources.
/// A VFR source muxed into HLS/DASH without -fps_mode cfr drifts its segment
/// durations off the -hls_time/-seg_duration target and desyncs across an ABR
/// switch (jellyfin #10485). CFR sources must be left alone.
/// </summary>
public class VfrEdgeCaseTests
{
    private readonly BuildStage _stage;

    public VfrEdgeCaseTests()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        _stage = new(
            options: options,
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
    }

    [Fact]
    public async Task VfrNormalized_HlsOutput_EmitsFpsModeCfr()
    {
        string[] args = await BuildArgs(format: OutputFormat.Hls, normalizeCfr: true);

        int idx = Array.IndexOf(array: args, value: "-fps_mode");
        idx.Should()
            .BeGreaterThan(expected: -1, because: "a VFR source must be muxed at a constant frame rate for HLS");
        args[idx + 1].Should().Be(expected: "cfr");
    }

    [Fact]
    public async Task CfrSource_HlsOutput_NoFpsMode()
    {
        string[] args = await BuildArgs(format: OutputFormat.Hls, normalizeCfr: false);

        args.Should().NotContain(unexpected: "-fps_mode", because: "a constant-frame-rate source needs no reshaping");
    }

    [Fact]
    public async Task VfrNormalized_DashOutput_EmitsFpsModeCfr()
    {
        string[] args = await BuildArgs(format: OutputFormat.Dash, normalizeCfr: true);

        int idx = Array.IndexOf(array: args, value: "-fps_mode");
        idx.Should()
            .BeGreaterThan(expected: -1, because: "a VFR source must be muxed at a constant frame rate for DASH");
        args[idx + 1].Should().Be(expected: "cfr");
    }

    [Fact]
    public void IsVariableFrameRate_TrueOnlyWhenRealAndAverageDiffer()
    {
        BuildVideoStream(real: 30.0, avg: 24.0).IsVariableFrameRate.Should().BeTrue();
        BuildVideoStream(real: 24.0, avg: 24.0).IsVariableFrameRate.Should().BeFalse();
    }

    private async Task<string[]> BuildArgs(OutputFormat format, bool normalizeCfr)
    {
        OutputPlan outputPlan = new(
            Format: format,
            VideoOutputs: [BuildVideoOutput(width: 1280, height: 720, mapLabel: "[v0]")],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null,
            NormalizeToConstantFrameRate: normalizeCfr
        );
        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(CorrelationId: EncodingContext.Create().CorrelationId, MediaInfo: BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        return ((StageSuccess<FfmpegCommand[]>)result).Value[0].Arguments;
    }

    private static ExecutionPlan BuildPlan(OutputPlan outputPlan) =>
        new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes: [new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new())],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 90),
            OutputPlan: outputPlan
        );

    private static VideoOutputPlan BuildVideoOutput(int width, int height, string mapLabel) =>
        new(
            Width: width,
            Height: height,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: mapLabel,
            ExtraFlags: new()
        );

    private static AudioOutputPlan BuildAudioOutput() =>
        new(
            EncoderName: "aac",
            BitrateKbps: 192,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: "en",
            MapLabel: "0:a:0"
        );

    private static VideoStreamInfo BuildVideoStream(double real, double avg) =>
        new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: real,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: true,
            BitRateKbps: 6000,
            AverageFrameRate: avg,
            RealFrameRate: real
        );

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams: [BuildVideoStream(real: 30.0, avg: 24.0)],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );
}
