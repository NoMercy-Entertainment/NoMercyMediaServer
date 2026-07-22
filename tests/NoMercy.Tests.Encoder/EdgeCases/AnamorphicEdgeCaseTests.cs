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
/// Guards anamorphic (non-square-pixel) handling. A source with SAR != 1:1
/// (e.g. a 720x576 DVD at SAR 64:45 for a 16:9 display) scaled by the ladder,
/// which works in square pixels, comes out stretched unless the pixels are
/// squared and SAR reset to 1:1 first (jellyfin #16665). Square-pixel and
/// unknown-SAR sources must be left alone.
/// </summary>
public class AnamorphicEdgeCaseTests
{
    private readonly BuildStage _stage;

    public AnamorphicEdgeCaseTests()
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
    public async Task AnamorphicSource_SquaresPixelsAndResetsSar()
    {
        string filter = await BuildFilterGraph(media: BuildMediaInfo(width: 720, height: 576, sar: "64:45"), outWidth: 1280, outHeight: 720);

        filter.Should().Contain(expected: "setsar=1", because: "anamorphic output must resolve to square pixels");
        filter
            .Should()
            .Contain(
                expected: "iw*64/45",
                because: "the width is scaled by the sample aspect ratio to square the pixels"
            );
    }

    [Fact]
    public async Task SquarePixelSource_NoSetSar()
    {
        string filter = await BuildFilterGraph(media: BuildMediaInfo(width: 1920, height: 1080, sar: "1:1"), outWidth: 1280, outHeight: 720);

        filter.Should().NotContain(unexpected: "setsar=1", because: "a square-pixel source needs no un-anamorph pass");
    }

    [Fact]
    public async Task UnknownSar_TreatedAsSquare_NoSetSar()
    {
        // ffprobe reports "0:1" when SAR is unknown — treat as square.
        string filter = await BuildFilterGraph(media: BuildMediaInfo(width: 1920, height: 1080, sar: "0:1"), outWidth: 1280, outHeight: 720);

        filter.Should().NotContain(unexpected: "setsar=1");
    }

    [Fact]
    public void IsAnamorphic_TrueOnlyForKnownNonSquareSar()
    {
        BuildVideoStream(sar: "64:45").IsAnamorphic.Should().BeTrue();
        BuildVideoStream(sar: "1:1").IsAnamorphic.Should().BeFalse();
        BuildVideoStream(sar: "0:1").IsAnamorphic.Should().BeFalse();
        BuildVideoStream(sar: null).IsAnamorphic.Should().BeFalse();
    }

    private async Task<string> BuildFilterGraph(MediaInfo media, int outWidth, int outHeight)
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: outWidth, height: outHeight, mapLabel: "[v0]")],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(CorrelationId: EncodingContext.Create().CorrelationId, MediaInfo: media);

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int idx = Array.IndexOf(array: commands[0].Arguments, value: "-filter_complex");
        idx.Should().BeGreaterThan(expected: -1, because: "a scaled output must build a filter graph");
        return commands[0].Arguments[idx + 1];
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

    private static VideoStreamInfo BuildVideoStream(string? sar) =>
        new(
            Index: 0,
            Codec: "mpeg2video",
            Width: 720,
            Height: 576,
            FrameRate: 25.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: true,
            BitRateKbps: 6000,
            SampleAspectRatio: sar
        );

    private static MediaInfo BuildMediaInfo(int width, int height, string? sar) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 4_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "mpeg2video",
                    Width: width,
                    Height: height,
                    FrameRate: 25.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000,
                    SampleAspectRatio: sar
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );
}
