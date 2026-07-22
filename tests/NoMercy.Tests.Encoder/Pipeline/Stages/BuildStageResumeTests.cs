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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Tests for the single-pass seek-to-keyframe resume feature.
/// When <see cref="BuildInput.ResumeFromMs"/> is set, BuildStage applies
/// an input seek (<c>-ss</c>) before the primary <c>-i</c> argument.
/// The seek is backed off by a fixed keyframe window so the encoder lands
/// on a clean keyframe rather than a mid-GOP position.
/// </summary>
public class BuildStageResumeTests
{
    private readonly BuildStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public BuildStageResumeTests()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };
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

    // ── ResolveResumeSeek unit tests ───────────────────────────────────────

    [Fact]
    public void ResolveResumeSeek_NullInput_ReturnsNull()
    {
        TimeSpan? result = BuildStage.ResolveResumeSeek(resumeFromMs: null);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveResumeSeek_ZeroInput_ReturnsNull()
    {
        TimeSpan? result = BuildStage.ResolveResumeSeek(resumeFromMs: 0);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveResumeSeek_SmallValueUnderBackoff_ClampsToZero()
    {
        // 2000ms < 4000ms backoff → seek to 0 (don't seek before start)
        TimeSpan? result = BuildStage.ResolveResumeSeek(resumeFromMs: 2000);
        result.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public void ResolveResumeSeek_ValueAboveBackoff_AppliesBackoff()
    {
        // 60000ms - 4000ms backoff = 56000ms seek position
        TimeSpan? result = BuildStage.ResolveResumeSeek(resumeFromMs: 60_000);
        result.Should().Be(expected: TimeSpan.FromMilliseconds(milliseconds: 56_000));
    }

    [Fact]
    public void ResolveResumeSeek_ExactlyAtBackoff_ClampsToZero()
    {
        TimeSpan? result = BuildStage.ResolveResumeSeek(resumeFromMs: 4000);
        result.Should().Be(expected: TimeSpan.Zero);
    }

    // ── Integration: ResumeFromMs wires -ss into the primary input ─────────

    [Fact]
    public async Task BuildStage_WithResumeFromMs_EmitsInputSeekBeforePrimaryInput()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"bsresume_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            BuildInput input = MakeBuildInput(tempDir: tempDir, resumeFromMs: 60_000);

            StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            string[] args = commands[0].Arguments;

            args.Should().Contain(expected: "-ss");
            // Backoff: 60000 - 4000 = 56000ms = 56.000s
            args.Should().Contain(expected: "56.000");
            // -ss must appear BEFORE -i (input seek, not output seek)
            int ssIdx = Array.IndexOf(array: args, value: "-ss");
            int iIdx = Array.IndexOf(array: args, value: "-i");
            ssIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
            ssIdx.Should().BeLessThan(expected: iIdx);
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task BuildStage_WithoutResumeFromMs_NoSeekEmitted()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"bsresume_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            BuildInput input = MakeBuildInput(tempDir: tempDir, resumeFromMs: null);

            StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            string[] args = commands[0].Arguments;

            args.Should().NotContain(unexpected: "-ss");
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    private static BuildInput MakeBuildInput(string tempDir, long? resumeFromMs)
    {
        ExecutionPlan plan = new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes:
                    [
                        new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                        new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 90),
            OutputPlan: new(
                Format: OutputFormat.Hls,
                VideoOutputs:
                [
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
                    ),
                ],
                AudioOutputs:
                [
                    new(
                        EncoderName: "aac",
                        BitrateKbps: 192,
                        Channels: 2,
                        SampleRate: 48000,
                        Action: StreamAction.Transcode,
                        Language: "en",
                        MapLabel: "0:a:0"
                    ),
                ],
                SubtitleOutputs: [],
                Thumbnails: null
            )
        );

        return new(
            Plan: plan,
            InputPath: "/dev/null",
            OutputDirectory: tempDir,
            MediaTitle: "test",
            ResumeFromMs: resumeFromMs
        );
    }
}
