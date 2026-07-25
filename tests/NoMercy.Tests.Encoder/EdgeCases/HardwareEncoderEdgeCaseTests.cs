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
            [
                new(
                    "group_0",
                    [new("decode_0", OperationType.Decode, [], new())],
                    null,
                    0,
                    4,
                    false,
                    1
                ),
            ],
            TimeSpan.FromMinutes(90),
            outputPlan
        );

    private static VideoOutputPlan BuildVideoOutput(
        int width,
        int height,
        string mapLabel,
        string encoder = "libx264"
    ) =>
        new(
            width,
            height,
            encoder,
            23,
            4000,
            "medium",
            "high",
            "4.1",
            false,
            "yuv420p",
            mapLabel,
            new()
        );

    private static AudioOutputPlan BuildAudioOutput() =>
        new(
            "aac",
            192,
            2,
            48000,
            StreamAction.Transcode,
            "en",
            "0:a:0"
        );

    private static MediaInfo BuildSdrMediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static MediaInfo BuildHdr10MediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            50000,
            30_000_000_000,
            [
                new(
                    0,
                    "hevc",
                    3840,
                    2160,
                    24.0,
                    10,
                    "yuv420p10le",
                    "bt2020",
                    "smpte2084",
                    "bt2020nc",
                    true,
                    45000
                ),
            ],
            [],
            [],
            []
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
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [],
            null
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
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [],
            null
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
            OutputFormat.Hls,
            [hdrPassthrough, sdrOutput],
            [BuildAudioOutput()],
            [],
            null
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
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [],
            null
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
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [],
            null
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
