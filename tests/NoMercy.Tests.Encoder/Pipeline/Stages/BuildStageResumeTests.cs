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
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            OutputStrategyFactoryTestHelper.Create(),
            [],
            NullLogger<BuildStage>.Instance,
            TestStorageFactory.CreateLocal()
        );
    }

    // ── ResolveResumeSeek unit tests ───────────────────────────────────────

    [Fact]
    public void ResolveResumeSeek_NullInput_ReturnsNull()
    {
        TimeSpan? result = BuildStage.ResolveResumeSeek(null);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveResumeSeek_ZeroInput_ReturnsNull()
    {
        TimeSpan? result = BuildStage.ResolveResumeSeek(0);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveResumeSeek_SmallValueUnderBackoff_ClampsToZero()
    {
        // 2000ms < 4000ms backoff → seek to 0 (don't seek before start)
        TimeSpan? result = BuildStage.ResolveResumeSeek(2000);
        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ResolveResumeSeek_ValueAboveBackoff_AppliesBackoff()
    {
        // 60000ms - 4000ms backoff = 56000ms seek position
        TimeSpan? result = BuildStage.ResolveResumeSeek(60_000);
        result.Should().Be(TimeSpan.FromMilliseconds(56_000));
    }

    [Fact]
    public void ResolveResumeSeek_ExactlyAtBackoff_ClampsToZero()
    {
        TimeSpan? result = BuildStage.ResolveResumeSeek(4000);
        result.Should().Be(TimeSpan.Zero);
    }

    // ── Integration: ResumeFromMs wires -ss into the primary input ─────────

    [Fact]
    public async Task BuildStage_WithResumeFromMs_EmitsInputSeekBeforePrimaryInput()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"bsresume_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            BuildInput input = MakeBuildInput(tempDir: tempDir, resumeFromMs: 60_000);

            StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            string[] args = commands[0].Arguments;

            args.Should().Contain("-ss");
            // Backoff: 60000 - 4000 = 56000ms = 56.000s
            args.Should().Contain("56.000");
            // -ss must appear BEFORE -i (input seek, not output seek)
            int ssIdx = Array.IndexOf(args, "-ss");
            int iIdx = Array.IndexOf(args, "-i");
            ssIdx.Should().BeGreaterThanOrEqualTo(0);
            ssIdx.Should().BeLessThan(iIdx);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task BuildStage_WithoutResumeFromMs_NoSeekEmitted()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"bsresume_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            BuildInput input = MakeBuildInput(tempDir: tempDir, resumeFromMs: null);

            StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            string[] args = commands[0].Arguments;

            args.Should().NotContain("-ss");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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
                        new("decode_0", OperationType.Decode, [], new()),
                        new("encode_0", OperationType.Encode, ["decode_0"], new()),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(90),
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
