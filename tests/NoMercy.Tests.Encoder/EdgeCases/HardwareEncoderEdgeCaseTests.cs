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

public class HardwareEncoderEdgeCaseTests
{
    private readonly BuildStage _stage;

    public HardwareEncoderEdgeCaseTests()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        _stage = new(
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            OutputStrategyFactoryTestHelper.Create(),
            [],
            NullLogger<BuildStage>.Instance,
            TestStorageFactory.CreateLocal()
        );
    }

    private static ExecutionPlan BuildPlan(OutputPlan outputPlan) =>
        new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes: [new("decode_0", OperationType.Decode, [], new())],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(90),
            OutputPlan: outputPlan
        );

    private static VideoOutputPlan BuildVideoOutput(
        int width,
        int height,
        string mapLabel,
        string encoder = "libx264"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoder,
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

    private static MediaInfo BuildSdrMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo BuildHdr10MediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 50000,
            FileSizeBytes: 30_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: 3840,
                    Height: 2160,
                    FrameRate: 24.0,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 45000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    [Fact]
    public async Task FilterGraphAssembler_SdrOutput_NoTonemapFilter()
    {
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]") with
        {
            ConvertHdrToSdr = false,
            TonemapFilterChain = null,
        };
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [videoOutput],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildSdrMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        if (filterComplexIdx > -1)
        {
            string filterValue = commands[0].Arguments[filterComplexIdx + 1];
            filterValue.Should().NotContain("tonemap");
            filterValue.Should().NotContain("zscale");
        }
    }

    [Fact]
    public async Task FilterGraphAssembler_HdrToSdrConversion_AppliesTonemapFilter()
    {
        const string tonemapChain =
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,"
            + "tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]") with
        {
            ConvertHdrToSdr = true,
            TonemapFilterChain = tonemapChain,
        };
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [videoOutput],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildHdr10MediaInfo()
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1);
        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue.Should().Contain("tonemap=hable");
    }

    [Fact]
    public async Task FilterGraphAssembler_MultipleHdrPassthroughRung_NoTonemapOnHdrBranch()
    {
        const string tonemapChain =
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,"
            + "tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";
        VideoOutputPlan hdrPassthrough = BuildVideoOutput(3840, 2160, "[v0]", "hevc_nvenc") with
        {
            TenBit = true,
            PixelFormat = "p010le",
            ConvertHdrToSdr = false,
        };
        VideoOutputPlan sdrOutput = BuildVideoOutput(1920, 1080, "[v1]") with
        {
            ConvertHdrToSdr = true,
            TonemapFilterChain = tonemapChain,
        };
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [hdrPassthrough, sdrOutput],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildHdr10MediaInfo()
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1);
        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue.Should().Contain("tonemap=hable");
        filterValue.Should().Contain("[v0]");
        filterValue.Should().Contain("[v1]");
    }

    [Fact]
    public async Task FilterGraphAssembler_SourceAndOutputSameDimensions_UseCopyFilter()
    {
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]");
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [videoOutput],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildSdrMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        if (filterComplexIdx > -1)
        {
            string filterValue = commands[0].Arguments[filterComplexIdx + 1];
            filterValue.Should().Contain("copy");
        }
    }

    [Fact]
    public async Task FilterGraphAssembler_HardwareEncoderName_EmittedInCommand()
    {
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]", "hevc_nvenc") with
        {
            EncoderName = "hevc_nvenc",
        };
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [videoOutput],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildSdrMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int codecIdx = Array.IndexOf(commands[0].Arguments, "-c:v");
        codecIdx.Should().BeGreaterThan(-1);
        string encoderName = commands[0].Arguments[codecIdx + 1];
        encoderName.Should().Be("hevc_nvenc");
    }
}
