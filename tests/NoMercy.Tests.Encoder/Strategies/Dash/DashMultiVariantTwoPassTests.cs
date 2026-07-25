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
        _outputDir = Path.Combine(Path.GetTempPath(), $"DashMultiVar_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass1OncePerVariant()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        List<int> pass1Indices = [];
        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (EncodingRequest req, IProgressObserver? _, CancellationToken _) =>
                {
                    if (req.Options!.Pass == EncodingPass.One)
                        pass1Indices.Add(req.Options.Pass1VariantIndex);
                    return Success();
                }
            );

        DashTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(3), null, CancellationToken.None);

        Assert.Equal(new[] { 0, 1, 2 }, pass1Indices);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass2ExactlyOnce()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        DashTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(3), null, CancellationToken.None);

        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Encode_ResumeWithAllVariantStatsPresent_SkipsPass1()
    {
        string statsDir = Path.Combine(_outputDir, ".2pass");
        Directory.CreateDirectory(statsDir);
        string statsFile = Path.Combine(statsDir, "x264");
        for (int i = 0; i < 3; i++)
            await File.WriteAllTextAsync($"{statsFile}_v{i}-0.log", $"variant {i} done");

        JobCheckpoint existing = new(
            "job-1",
            "/media/src.mkv",
            _outputDir,
            [],
            DateTime.UtcNow,
            statsFile,
            true,
            -1,
            "TwoPass"
        );

        _checkpointStore
            .Setup(s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupSuccessfulEncoder();

        DashTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(3), null, CancellationToken.None);

        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    private void SetupSuccessfulEncoder()
    {
        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Success());
    }

    private static EncodingResult Success() =>
        new(
            true,
            "/out",
            TimeSpan.FromSeconds(1),
            null,
            new(1024, 2.0, 24.0, "libx264", null)
        );

    private DashTwoPassStrategy BuildStrategy() =>
        new(
            _encoder.Object,
            _checkpointStore.Object,
            NullLogger<DashTwoPassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

    private EncodingRequest BuildRequest(int variantCount) =>
        new(
            "/media/src.mkv",
            _outputDir,
            new(
                Ulid.NewUlid(),
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
                        .Range(0, variantCount)
                        .Select(i => new LadderRung(
                            1920 >> i,
                            1080 >> i,
                            VideoCodecType.H264,
                            4000 >> i,
                            6000 >> i,
                            8000 >> i,
                            24.0,
                            "medium",
                            CodecProfile.High,
                            8,
                            "yuv420p"
                        ))
                        .ToArray(),
                }
            )
        );
}
