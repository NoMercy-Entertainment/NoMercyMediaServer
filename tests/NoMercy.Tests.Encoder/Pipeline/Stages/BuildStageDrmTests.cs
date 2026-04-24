namespace NoMercy.Tests.Encoder.Pipeline.Stages;

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
using NoMercy.Tests.Encoder.Storage;

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

            DrmConfig drm = new(DrmMethod.Aes128, KeyUri: "https://example/keys/1");
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
            commands[0].Arguments[idx + 1].Should().EndWith("drm_keyinfo.txt");

            // Artifacts must land on disk in the output directory.
            File.Exists(Path.Combine(tempDir, "drm.key")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "drm_keyinfo.txt")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
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

            OutputPlan plan = BuildPlan(drm: null);
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
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Drm_NoMatchingProcessor_ContinuesWithoutEncryption()
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
            // registered to handle it. Build should warn + continue.
            BuildStage stage = new(
                options,
                new FontExtractor(TestStorageFactory.CreateLocal()),
                new SubtitleExtractor(),
                OutputStrategyFactoryTestHelper.Create(),
                [],
                NullLogger<BuildStage>.Instance,
                TestStorageFactory.CreateLocal()
            );

            DrmConfig drm = new(DrmMethod.Aes128, KeyUri: "https://example/keys/1");
            OutputPlan plan = BuildPlan(drm);
            ExecutionPlan execPlan = BuildExecutionPlan(plan);
            BuildInput input = new(execPlan, "/movies/test.mkv", tempDir, "TestMovie");
            EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

            StageResult result = await stage.ExecuteAsync(input, context, default);

            result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
            commands[0].Arguments.Should().NotContain("-hls_key_info_file");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
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

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
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
