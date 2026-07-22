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
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"drm-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            EncoderOptions options = new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
            };

            BuildStage stage = new(
                options: options,
                fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
                subtitleExtractor: new SubtitleExtractor(),
                outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
                drmProcessors: [new Aes128HlsDrmProcessor(storage: TestStorageFactory.CreateLocal())],
                logger: NullLogger<BuildStage>.Instance,
                storage: TestStorageFactory.CreateLocal()
            );

            DrmConfig drm = new(Method: DrmMethod.Aes128, KeyUri: "https://example/keys/1");
            OutputPlan plan = BuildPlan(drm: drm);
            ExecutionPlan execPlan = BuildExecutionPlan(outputPlan: plan);
            BuildInput input = new(Plan: execPlan, InputPath: "/movies/test.mkv", OutputDirectory: tempDir, MediaTitle: "TestMovie");
            EncodingContext context = new(CorrelationId: EncodingContext.Create().CorrelationId, MediaInfo: BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

            // The main ffmpeg command must carry -hls_key_info_file pointing at
            // the prepared keyinfo file.
            int idx = Array.IndexOf(array: commands[0].Arguments, value: "-hls_key_info_file");
            idx.Should().BeGreaterThan(expected: -1, because: "DRM processor should inject the flag");
            string keyInfoPath = commands[0].Arguments[idx + 1];
            keyInfoPath.Should().EndWith(expected: "drm_keyinfo.txt");

            // The raw key must NEVER land in the published output directory —
            // it would ship next to the ciphertext it's meant to protect.
            File.Exists(path: Path.Combine(path1: tempDir, path2: "drm.key")).Should().BeFalse();
            File.Exists(path: Path.Combine(path1: tempDir, path2: "drm_keyinfo.txt")).Should().BeFalse();

            // Artifacts land in a per-encode temp directory outside TempRoot's
            // sibling published dirs, and are readable there for ffmpeg.
            string fullKeyInfoDir = Path.GetFullPath(path: Path.GetDirectoryName(path: keyInfoPath)!);
            string fullTempRoot = Path.GetFullPath(path: StoragePaths.TempRoot);
            fullKeyInfoDir
                .Should()
                .StartWith(
                    expected: fullTempRoot,
                    because: "DRM key artifacts must live under TempRoot, never the published output dir"
                );
            File.Exists(path: keyInfoPath).Should().BeTrue();
            File.Exists(path: Path.Combine(path1: fullKeyInfoDir, path2: "drm.key")).Should().BeTrue();

            Directory.Delete(path: fullKeyInfoDir, recursive: true);
        }
        finally
        {
            if (Directory.Exists(path: tempDir))
                Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NoDrm_DoesNotInjectKeyInfoFlag()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"drm-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            EncoderOptions options = new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
            };

            BuildStage stage = new(
                options: options,
                fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
                subtitleExtractor: new SubtitleExtractor(),
                outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
                drmProcessors: [new Aes128HlsDrmProcessor(storage: TestStorageFactory.CreateLocal())],
                logger: NullLogger<BuildStage>.Instance,
                storage: TestStorageFactory.CreateLocal()
            );

            OutputPlan plan = BuildPlan(drm: null);
            ExecutionPlan execPlan = BuildExecutionPlan(outputPlan: plan);
            BuildInput input = new(Plan: execPlan, InputPath: "/movies/test.mkv", OutputDirectory: tempDir, MediaTitle: "TestMovie");
            EncodingContext context = new(CorrelationId: EncodingContext.Create().CorrelationId, MediaInfo: BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            commands[0].Arguments.Should().NotContain(unexpected: "-hls_key_info_file");

            File.Exists(path: Path.Combine(path1: tempDir, path2: "drm.key")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(path: tempDir))
                Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Drm_NoMatchingProcessor_FailsTheEncode()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"drm-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

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
                options: options,
                fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
                subtitleExtractor: new SubtitleExtractor(),
                outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
                drmProcessors: [],
                logger: NullLogger<BuildStage>.Instance,
                storage: TestStorageFactory.CreateLocal()
            );

            DrmConfig drm = new(Method: DrmMethod.Aes128, KeyUri: "https://example/keys/1");
            OutputPlan plan = BuildPlan(drm: drm);
            ExecutionPlan execPlan = BuildExecutionPlan(outputPlan: plan);
            BuildInput input = new(Plan: execPlan, InputPath: "/movies/test.mkv", OutputDirectory: tempDir, MediaTitle: "TestMovie");
            EncodingContext context = new(CorrelationId: EncodingContext.Create().CorrelationId, MediaInfo: BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

            result.Should().BeOfType<StageFailure>();
            StageFailure failure = (StageFailure)result;
            failure.Error.Message.Should().Contain(expected: "Aes128");
        }
        finally
        {
            if (Directory.Exists(path: tempDir))
                Directory.Delete(path: tempDir, recursive: true);
        }
    }

    private static OutputPlan BuildPlan(DrmConfig? drm) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1280,
                    Height: 720,
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
            Thumbnails: null,
            Drm: drm
        );

    private static ExecutionPlan BuildExecutionPlan(OutputPlan outputPlan) =>
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

    private static MediaInfo BuildMediaInfo() =>
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
}
