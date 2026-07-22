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

public class PixelFormatEdgeCaseTests
{
    private readonly BuildStage _stage;

    public PixelFormatEdgeCaseTests()
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

    private static VideoOutputPlan BuildVideoOutput(
        int width,
        int height,
        string mapLabel,
        string encoder = "libx264",
        bool tenBit = false,
        string pixelFormat = "yuv420p"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoder,
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: tenBit ? "high10" : "high",
            Level: "4.1",
            TenBit: tenBit,
            PixelFormat: pixelFormat,
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

    private static MediaInfo Build10BitMediaInfo(int width = 3840, int height = 2160) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 50000,
            FileSizeBytes: 30_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 45000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo Build8BitMediaInfo(int width = 1920, int height = 1080) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    [Fact]
    public async Task BuildStage_10BitSourceTo8BitProfile_OutputPixelFormatIsEightBit()
    {
        VideoOutputPlan output = BuildVideoOutput(
            width: 1280,
            height: 720,
            mapLabel: "[v0]",
            encoder: "libx264",
            tenBit: false,
            pixelFormat: "yuv420p"
        );

        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [output],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: Build10BitMediaInfo(width: 3840, height: 2160)
        );

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(array: commands[0].Arguments, value: "-filter_complex");
        filterComplexIdx
            .Should()
            .BeGreaterThan(
                expected: -1,
                because: "scaling from 10-bit source to 8-bit target requires filter_complex"
            );

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue
            .Should()
            .Contain(
                expected: "format=yuv420p",
                because: "8-bit target must output yuv420p, not 10-bit p010 or p010le"
            );
        filterValue.Should().NotContain(unexpected: "p010", because: "8-bit target must not output 10-bit pixel format");
    }

    [Fact]
    public async Task BuildStage_OutputWithOddDimensions_MakeDimensionsEven()
    {
        VideoOutputPlan output = BuildVideoOutput(
            width: 1279,
            height: 719,
            mapLabel: "[v0]",
            encoder: "libx264",
            tenBit: false,
            pixelFormat: "yuv420p"
        );

        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [output],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: Build8BitMediaInfo(width: 1920, height: 1080)
        );

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(array: commands[0].Arguments, value: "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(expected: -1, because: "filter_complex must be present when scaling");

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];

        filterValue
            .Should()
            .Contain(expected: "scale=1279:-2", because: "requested 1279 width must be preserved in scale filter");

        filterValue.Should().NotContain(unexpected: "1279:719", because: "odd dimensions should not be preserved as-is");
    }
}
