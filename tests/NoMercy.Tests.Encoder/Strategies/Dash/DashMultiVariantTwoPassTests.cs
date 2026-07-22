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
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Tests.Encoder.Storage;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using LadderRung = NoMercy.Encoder.Profiles.LadderRung;

namespace NoMercy.Tests.Encoder.Strategies.Dash;

/// <summary>
/// DASH adaptive ladders need the same multi-variant 2-pass treatment as HLS:
/// pass 1 runs once per variant with per-variant stats files, pass 2 runs
/// once producing every variant in the same ffmpeg invocation. These tests
/// mirror the HLS coverage to confirm the shared <c>TwoPassStrategyBase</c>
/// logic really does apply uniformly across adaptive formats.
/// </summary>
public class DashMultiVariantTwoPassTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public DashMultiVariantTwoPassTests()
    {
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"DashMultiVar_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass1OncePerVariant()
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

        DashTwoPassStrategy strategy = BuildStrategy();
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

        DashTwoPassStrategy strategy = BuildStrategy();
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
    public async Task Encode_ResumeWithAllVariantStatsPresent_SkipsPass1()
    {
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

        DashTwoPassStrategy strategy = BuildStrategy();
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

    private DashTwoPassStrategy BuildStrategy() =>
        new(
            encoder: _encoder.Object,
            checkpointStore: _checkpointStore.Object,
            logger: NullLogger<DashTwoPassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

    private EncodingRequest BuildRequest(int variantCount) =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: $"DASH 2-pass {variantCount}-variant",
                Container: Container.Dash,
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
