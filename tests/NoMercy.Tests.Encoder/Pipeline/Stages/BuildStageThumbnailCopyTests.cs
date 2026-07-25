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
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// A stream-copied video output has no filtergraph, so it cannot carry the
/// inline "-map [thumbs]" sprite output the transcode path uses (ffmpeg exit
/// -22: "Streamcopy requested for output stream fed from a complex
/// filtergraph"). BuildStage must instead build the sprite as a SEPARATE
/// command reading the source directly, so a remux preset keeps its scrub
/// thumbnails instead of silently losing them.
/// </summary>
public class BuildStageThumbnailCopyTests
{
    private readonly BuildStage _stage;

    public BuildStageThumbnailCopyTests()
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

    private static MediaInfo BuildMediaInfo(int width = 1920, int height = 1080) =>
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
                    Width: width,
                    Height: height,
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

    private static MediaInfo BuildHdrMediaInfo(int width = 3840, int height = 2160) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 20000,
            FileSizeBytes: 18_000_000_000,
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
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 18000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static VideoOutputPlan BuildCopyVideoOutput() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "copy",
            Crf: 0,
            BitrateKbps: 0,
            Preset: null,
            Profile: null,
            Level: null,
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "0:v:0",
            ExtraFlags: new()
        );

    private static VideoOutputPlan BuildTranscodeVideoOutput() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
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

    [Fact]
    public async Task CopyVideoPlan_MainCommandHasNoInlineThumbsMap()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildCopyVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        FfmpegCommand mainCommand = commands[0];
        mainCommand
            .Arguments.Should()
            .NotContain("[thumbs]", "a copy video has no filtergraph pad");
        mainCommand.Arguments.Should().NotContain("-filter_complex");
    }

    [Fact]
    public async Task CopyVideoPlan_EmitsSeparateSpriteCommandReadingSource()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildCopyVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        FfmpegCommand? spriteCommand = commands.FirstOrDefault(c =>
            c.Arguments.Contains("spritevtt")
        );

        spriteCommand.Should().NotBeNull("a copy-video plan must still produce the sprite command");
        spriteCommand!.Arguments.Should().Contain("/movies/test.mkv");
        spriteCommand.Arguments.Should().Contain("thumbs_160x90.webp");
        spriteCommand.Arguments.Should().Contain("thumbs_160x90.vtt");

        int vfIdx = Array.IndexOf(spriteCommand.Arguments, "-vf");
        vfIdx
            .Should()
            .BeGreaterThan(-1, "the sprite is scaled/sampled via a plain -vf on direct input");
        spriteCommand.Arguments[vfIdx + 1].Should().Contain("fps=1/10");
        spriteCommand.Arguments[vfIdx + 1].Should().Contain("scale=160:-2");

        spriteCommand.Arguments.Should().NotContain("-filter_complex");
    }

    [Fact]
    public async Task CopyVideoPlan_SpriteFilenamesMatchInlineTranscodePathNaming()
    {
        ThumbnailOutputPlan thumbs = new(160, 90, 10);

        OutputPlan copyPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildCopyVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: thumbs
        );
        OutputPlan transcodePlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildTranscodeVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: thumbs
        );

        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult copyResult = await _stage.ExecuteAsync(
            new(BuildPlan(copyPlan), "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy"),
            context,
            default
        );
        StageResult transcodeResult = await _stage.ExecuteAsync(
            new(
                BuildPlan(transcodePlan),
                "/movies/test.mkv",
                "/tmp/nmtest-output/test",
                "Test.NoMercy"
            ),
            context,
            default
        );

        FfmpegCommand copySprite = ((StageSuccess<FfmpegCommand[]>)copyResult).Value.First(c =>
            c.Arguments.Contains("spritevtt")
        );
        FfmpegCommand transcodeMain = ((StageSuccess<FfmpegCommand[]>)transcodeResult).Value[0];

        copySprite.Arguments.Should().Contain("thumbs_160x90.webp");
        copySprite.Arguments.Should().Contain("thumbs_160x90.vtt");
        transcodeMain.Arguments.Should().Contain("thumbs_160x90.webp");
        transcodeMain.Arguments.Should().Contain("thumbs_160x90.vtt");
    }

    [Fact]
    public async Task TranscodeVideoPlan_StillEmitsInlineThumbsMap()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildTranscodeVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        FfmpegCommand mainCommand = commands[0];

        int filterComplexIdx = Array.IndexOf(mainCommand.Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1);
        mainCommand.Arguments[filterComplexIdx + 1].Should().Contain("thumbs");

        mainCommand.Arguments.Should().Contain("[thumbs]");

        commands.Should().NotContain(c => c.Arguments.Contains("spritevtt") && c != mainCommand);
    }

    // ── Decomposed bundle with no video output ───────────────────────────────
    // A decomposed encode can produce a bundle that carries only an audio or
    // subtitle rung. It has no video output at all, so — exactly as with a
    // stream copy — nothing defines the [thumbs] pad. Mapping it anyway makes
    // ffmpeg reject the whole command ("Output with label 'thumbs' does not
    // exist in any defined filter graph" → exit -22), which fails the task and
    // makes VideoEncodeJob skip post-encode, taking subtitle OCR down with it.

    [Fact]
    public async Task NoVideoOutputs_MainCommandHasNoInlineThumbsMap()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        commands[0]
            .Arguments.Should()
            .NotContain(
                "[thumbs]",
                "a bundle with no video output defines no filtergraph pad to map"
            );
    }

    [Fact]
    public async Task NoVideoOutputs_EmitsSeparateSpriteCommandReadingSource()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        FfmpegCommand sprite = commands.Single(c => c.Arguments.Contains("spritevtt"));

        // Reads the source's own video rather than a pad from the main command.
        sprite.Arguments.Should().Contain("0:v:0");
        sprite.Arguments.Should().NotContain("[thumbs]");
    }

    [Fact]
    public async Task ThumbnailsOnlyBundle_EmitsNoMainCommand_OnlyTheSprite()
    {
        // The shape a decomposed Thumbnails task produces: nothing to encode, one
        // sprite to write. Handing ffmpeg a command with no output at all makes it
        // exit 1 ("At least one output file must be specified"), which fails the
        // task and costs the job its whole post-encode phase.
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        commands
            .Should()
            .ContainSingle("only the sprite command has anything to write")
            .Which.Arguments.Should()
            .Contain("spritevtt");
    }

    // ── Standalone sprite color pipeline (HDR vs SDR source) ─────────────────
    // The standalone sprite command (copy-video / no-video branch above) has no
    // filtergraph rung to borrow a tonemap chain from. Sampling raw HDR through
    // a bare -vf produces crushed, wrong colours — ThumbnailFilterResolver is
    // the one place that decision lives, and BuildStage must actually call it
    // instead of hardcoding a tonemap-blind filter string.

    [Fact]
    public async Task CopyVideoPlan_HdrSource_SpriteCommandIncludesTonemapChain()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildCopyVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildHdrMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        FfmpegCommand sprite = ((StageSuccess<FfmpegCommand[]>)result).Value.Single(c =>
            c.Arguments.Contains("spritevtt")
        );

        int vfIdx = Array.IndexOf(sprite.Arguments, "-vf");
        vfIdx.Should().BeGreaterThan(-1);
        sprite
            .Arguments[vfIdx + 1]
            .Should()
            .Contain(
                "tonemap",
                "an HDR source sampled without tonemap produces crushed, wrong-coloured thumbnails"
            );
        sprite.Arguments[vfIdx + 1].Should().Contain("fps=1/10");
        sprite.Arguments[vfIdx + 1].Should().Contain("scale=160:-2");
    }

    [Fact]
    public async Task CopyVideoPlan_SdrSource_SpriteCommandHasBareFilter_NoTonemap()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildCopyVideoOutput()],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        FfmpegCommand sprite = ((StageSuccess<FfmpegCommand[]>)result).Value.Single(c =>
            c.Arguments.Contains("spritevtt")
        );

        int vfIdx = Array.IndexOf(sprite.Arguments, "-vf");
        sprite
            .Arguments[vfIdx + 1]
            .Should()
            .NotContain("tonemap", "an SDR source needs no HDR→SDR conversion");
    }

    [Fact]
    public async Task NoVideoOutputs_HdrSource_SpriteCommandIncludesTonemapChain()
    {
        // Same standalone-sprite branch fires for the zero-video-output bundle
        // (an audio/subtitle-only decomposed task) — it must resolve the same
        // HDR-aware filter as the copy-video case above.
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildHdrMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        FfmpegCommand sprite = ((StageSuccess<FfmpegCommand[]>)result).Value.Single(c =>
            c.Arguments.Contains("spritevtt")
        );

        int vfIdx = Array.IndexOf(sprite.Arguments, "-vf");
        vfIdx.Should().BeGreaterThan(-1);
        sprite.Arguments[vfIdx + 1].Should().Contain("tonemap");
    }
}
