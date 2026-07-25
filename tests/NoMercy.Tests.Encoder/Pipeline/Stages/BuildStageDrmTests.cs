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
using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class BuildStageDrmTests
{
    [Fact]
    public async Task DrmAes128_InjectsKeyInfoFlagIntoVideoOutput()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"drm-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            EncoderOptions options = new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
            };

            BuildStage stage = new(
                options,
                new FontExtractor(TestStorageFactory.CreateLocal()),
                new SubtitleExtractor(),
                OutputStrategyFactoryTestHelper.Create(),
                [new Aes128HlsDrmProcessor(TestStorageFactory.CreateLocal())],
                NullLogger<BuildStage>.Instance,
                TestStorageFactory.CreateLocal()
            );

            DrmConfig drm = new(DrmMethod.Aes128, "https://example/keys/1");
            OutputPlan plan = BuildPlan(drm);
            ExecutionPlan execPlan = BuildExecutionPlan(plan);
            BuildInput input = new(execPlan, "/movies/test.mkv", tempDir, "TestMovie");
            EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input, context, default);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

            // The main ffmpeg command must carry -hls_key_info_file pointing at
            // the prepared keyinfo file.
            int idx = Array.IndexOf(commands[0].Arguments, "-hls_key_info_file");
            idx.Should().BeGreaterThan(-1, "DRM processor should inject the flag");
            string keyInfoPath = commands[0].Arguments[idx + 1];
            keyInfoPath.Should().EndWith("drm_keyinfo.txt");

            // The raw key must NEVER land in the published output directory —
            // it would ship next to the ciphertext it's meant to protect.
            File.Exists(Path.Combine(tempDir, "drm.key")).Should().BeFalse();
            File.Exists(Path.Combine(tempDir, "drm_keyinfo.txt")).Should().BeFalse();

            // Artifacts land in a per-encode temp directory outside TempRoot's
            // sibling published dirs, and are readable there for ffmpeg.
            string fullKeyInfoDir = Path.GetFullPath(Path.GetDirectoryName(keyInfoPath)!);
            string fullTempRoot = Path.GetFullPath(StoragePaths.TempRoot);
            fullKeyInfoDir
                .Should()
                .StartWith(
                    fullTempRoot,
                    "DRM key artifacts must live under TempRoot, never the published output dir"
                );
            File.Exists(keyInfoPath).Should().BeTrue();
            File.Exists(Path.Combine(fullKeyInfoDir, "drm.key")).Should().BeTrue();

            Directory.Delete(fullKeyInfoDir, true);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task NoDrm_DoesNotInjectKeyInfoFlag()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"drm-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            EncoderOptions options = new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
            };

            BuildStage stage = new(
                options,
                new FontExtractor(TestStorageFactory.CreateLocal()),
                new SubtitleExtractor(),
                OutputStrategyFactoryTestHelper.Create(),
                [new Aes128HlsDrmProcessor(TestStorageFactory.CreateLocal())],
                NullLogger<BuildStage>.Instance,
                TestStorageFactory.CreateLocal()
            );

            OutputPlan plan = BuildPlan(null);
            ExecutionPlan execPlan = BuildExecutionPlan(plan);
            BuildInput input = new(execPlan, "/movies/test.mkv", tempDir, "TestMovie");
            EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input, context, default);

            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            commands[0].Arguments.Should().NotContain("-hls_key_info_file");

            File.Exists(Path.Combine(tempDir, "drm.key")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Drm_NoMatchingProcessor_FailsTheEncode()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"drm-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            EncoderOptions options = new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
            };

            // Empty processor list — the profile asks for AES-128 but nothing
            // registered to handle it. Build must fail rather than silently
            // shipping an unencrypted encode while reporting success.
            BuildStage stage = new(
                options,
                new FontExtractor(TestStorageFactory.CreateLocal()),
                new SubtitleExtractor(),
                OutputStrategyFactoryTestHelper.Create(),
                [],
                NullLogger<BuildStage>.Instance,
                TestStorageFactory.CreateLocal()
            );

            DrmConfig drm = new(DrmMethod.Aes128, "https://example/keys/1");
            OutputPlan plan = BuildPlan(drm);
            ExecutionPlan execPlan = BuildExecutionPlan(plan);
            BuildInput input = new(execPlan, "/movies/test.mkv", tempDir, "TestMovie");
            EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input, context, default);

            result.Should().BeOfType<StageFailure>();
            StageFailure failure = (StageFailure)result;
            failure.Error.Message.Should().Contain("Aes128");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static OutputPlan BuildPlan(DrmConfig? drm) =>
        new(
            OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    1280,
                    720,
                    "libx264",
                    23,
                    4000,
                    "medium",
                    "high",
                    "4.1",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    "aac",
                    192,
                    2,
                    48000,
                    StreamAction.Transcode,
                    "en",
                    "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null,
            Drm: drm
        );

    private static ExecutionPlan BuildExecutionPlan(OutputPlan outputPlan) =>
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

    private static MediaInfo BuildMediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            4_000_000_000,
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
}
