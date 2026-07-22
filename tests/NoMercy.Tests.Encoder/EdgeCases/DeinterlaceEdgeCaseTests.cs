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
/// Guards deinterlace handling: an interlaced source scaled without a
/// deinterlace filter produces combing artifacts in the output (jellyfin
/// #4314). The deinterlace must run before the scale, once on the shared
/// source, and must NOT be added for progressive sources.
/// </summary>
public class DeinterlaceEdgeCaseTests
{
    private readonly BuildStage _stage;

    public DeinterlaceEdgeCaseTests()
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
    public async Task InterlacedSource_ScaledOutput_EmitsDeinterlaceBeforeScale()
    {
        string filter = await BuildFilterGraph(media: BuildMediaInfo(width: 1920, height: 1080, fieldOrder: "tt"), outWidth: 1280, outHeight: 720);

        filter
            .Should()
            .Contain(expected: "yadif", because: "interlaced source scaled to progressive must deinterlace");
        int deintIdx = filter.IndexOf(value: "yadif", comparisonType: StringComparison.Ordinal);
        int scaleIdx = filter.IndexOf(value: "scale=", comparisonType: StringComparison.Ordinal);
        deintIdx
            .Should()
            .BeLessThan(expected: scaleIdx, because: "deinterlace reconstructs full frames before scaling");
    }

    [Fact]
    public async Task ProgressiveSource_NoDeinterlaceFilter()
    {
        string filter = await BuildFilterGraph(
            media: BuildMediaInfo(width: 1920, height: 1080, fieldOrder: "progressive"),
            outWidth: 1280,
            outHeight: 720
        );

        filter.Should().NotContain(unexpected: "yadif", because: "a progressive source must never be deinterlaced");
    }

    [Fact]
    public async Task UnknownFieldOrder_NoDeinterlaceFilter()
    {
        // Absent field_order (null) is treated as progressive — do not deinterlace.
        string filter = await BuildFilterGraph(
            media: BuildMediaInfo(width: 1920, height: 1080, fieldOrder: null),
            outWidth: 1280,
            outHeight: 720
        );

        filter.Should().NotContain(unexpected: "yadif");
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

    private static MediaInfo BuildMediaInfo(int width, int height, string? fieldOrder) =>
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
                    FrameRate: 25.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000,
                    FieldOrder: fieldOrder
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );
}
