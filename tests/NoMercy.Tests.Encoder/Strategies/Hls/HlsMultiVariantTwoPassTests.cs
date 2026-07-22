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
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using LadderRung = NoMercy.Encoder.Profiles.LadderRung;

namespace NoMercy.Tests.Encoder.Strategies.Hls;

/// <summary>
/// Covers the multi-variant 2-pass path added in Tier 1.3 — pass 1 runs once
/// per variant (each with its own stats file), pass 2 runs once with all
/// variants sharing the same run.
/// </summary>
public class HlsMultiVariantTwoPassTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public HlsMultiVariantTwoPassTests()
    {
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"MultiVar_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass1ThreeTimesWithIncreasingIndex()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        List<int> pass1Indices = [];
        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                valueFunction: (EncodingRequest req, IProgressObserver? _, CancellationToken _) =>
                {
                    if (req.Options!.Pass == EncodingPass.One)
                        pass1Indices.Add(item: req.Options.Pass1VariantIndex);
                    return Success();
                }
            );

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(variantCount: 3), progress: null, ct: CancellationToken.None);

        Assert.Equal(expected: new[] { 0, 1, 2 }, actual: pass1Indices);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass2ExactlyOnce()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(variantCount: 3), progress: null, ct: CancellationToken.None);

        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task Encode_Pass1FailsOnSecondVariant_Pass2NotRun()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                valueFunction: (EncodingRequest req, IProgressObserver? _, CancellationToken _) =>
                {
                    if (req.Options!.Pass == EncodingPass.One && req.Options.Pass1VariantIndex == 1)
                        return Fail(message: "pass1 variant 1 exploded");
                    return Success();
                }
            );

        HlsTwoPassStrategy strategy = BuildStrategy();
        EncodingResult result = await strategy.EncodeAsync(
            request: BuildRequest(variantCount: 3),
            progress: null,
            ct: CancellationToken.None
        );

        Assert.False(condition: result.Success);
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
        // Pass 1 ran for variant 0 then failed on variant 1 → 2 calls, not 3.
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Exactly(callCount: 2)
        );
    }

    [Fact]
    public async Task Encode_ResumeWithAllVariantStatsPresent_SkipsPass1()
    {
        // All per-variant stats files must exist for resume to skip pass 1.
        string statsDir = Path.Combine(path1: _outputDir, path2: ".2pass");
        Directory.CreateDirectory(path: statsDir);
        string statsFile = Path.Combine(path1: statsDir, path2: "x264");
        for (int i = 0; i < 3; i++)
            await File.WriteAllTextAsync(path: $"{statsFile}_v{i}-0.log", contents: $"variant {i} done");

        JobCheckpoint existing = new(
            JobId: "job-1",
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFile,
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );

        _checkpointStore
            .Setup(expression: s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: existing);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(variantCount: 3), progress: null, ct: CancellationToken.None);

        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task Encode_ResumeWithPartialVariantStats_ReRunsAllPass1()
    {
        // Only variant 0's stats exists — variant 1 + 2 are missing. Pass 1
        // has to re-run for all variants; mixing stale and fresh stats across
        // variants gives unreliable quality.
        string statsDir = Path.Combine(path1: _outputDir, path2: ".2pass");
        Directory.CreateDirectory(path: statsDir);
        string statsFile = Path.Combine(path1: statsDir, path2: "x264");
        await File.WriteAllTextAsync(path: $"{statsFile}_v0-0.log", contents: "stale");

        JobCheckpoint existing = new(
            JobId: "job-1",
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFile,
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );

        _checkpointStore
            .Setup(expression: s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: existing);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(variantCount: 3), progress: null, ct: CancellationToken.None);

        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Exactly(callCount: 3)
        );
    }

    private void SetupSuccessfulEncoder()
    {
        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success());
    }

    private static EncodingResult Success() =>
        new(
            Success: true,
            OutputPath: "/out",
            Duration: TimeSpan.FromSeconds(seconds: 1),
            Error: null,
            Metrics: new(OutputSizeBytes: 1024, AverageSpeed: 2.0, AverageFps: 24.0, EncoderUsed: "libx264", GpuUsed: null)
        );

    private static EncodingResult Fail(string message) =>
        new(
            Success: false,
            OutputPath: string.Empty,
            Duration: TimeSpan.Zero,
            Error: new(Kind: EncodingErrorKind.ProcessCrashed, Message: message, FfmpegStderr: null, StageName: "Pass1", Recoverable: false),
            Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: string.Empty, GpuUsed: null)
        );

    private HlsTwoPassStrategy BuildStrategy() =>
        new(
            encoder: _encoder.Object,
            checkpointStore: _checkpointStore.Object,
            logger: NullLogger<HlsTwoPassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

    private EncodingRequest BuildRequest(int variantCount) =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: $"HLS 2-pass {variantCount}-variant",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.TwoPass,
                Ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs = Enumerable
                        .Range(start: 0, count: variantCount)
                        .Select(selector: i => new LadderRung(
                            Width: 1920 >> i,
                            Height: 1080 >> i,
                            Codec: VideoCodecType.H264,
                            BitrateKbps: 4000 >> i,
                            MaxBitrateKbps: 6000 >> i,
                            BufferSizeKbps: 8000 >> i,
                            Framerate: 24.0,
                            Preset: "medium",
                            CodecProfile: CodecProfile.High,
                            BitDepth: 8,
                            PixelFormat: "yuv420p"
                        ))
                        .ToArray(),
                }
            )
        );
}
