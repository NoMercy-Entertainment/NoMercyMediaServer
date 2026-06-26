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
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Encoder.Strategies.Mp4;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// Covers the MP4 / DASH siblings that share <see cref="TwoPassStrategyBase"/>
/// with <c>HlsTwoPassStrategy</c>. The shared orchestration is already covered
/// in depth by <c>HlsTwoPassStrategyTests</c> — here we only verify that each
/// sibling reports the correct format + mode and runs two passes through the
/// injected encoder.
/// </summary>
public class TwoPassStrategySiblingsTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public TwoPassStrategySiblingsTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"TwoPassSiblings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    public static TheoryData<IEncodingStrategy, OutputFormat> SiblingsWithFormat()
    {
        Mock<IEncoder> encoder = new();
        Mock<ICheckpointStore> store = new();
        return new()
        {
            {
                new HlsTwoPassStrategy(
                    encoder.Object,
                    store.Object,
                    NullLogger<HlsTwoPassStrategy>.Instance,
                    TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Hls
            },
            {
                new Mp4TwoPassStrategy(
                    encoder.Object,
                    store.Object,
                    NullLogger<Mp4TwoPassStrategy>.Instance,
                    TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Mp4
            },
            {
                new DashTwoPassStrategy(
                    encoder.Object,
                    store.Object,
                    NullLogger<DashTwoPassStrategy>.Instance,
                    TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Dash
            },
        };
    }

    [Theory]
    [MemberData(nameof(SiblingsWithFormat))]
    public void Siblings_ReportCorrectFormatAndTwoPassMode(
        IEncodingStrategy strategy,
        OutputFormat expectedFormat
    )
    {
        Assert.Equal(expectedFormat, strategy.Format);
        Assert.Equal(EncodeMode.TwoPass, strategy.EncodeMode);
    }

    [Theory]
    [InlineData(OutputFormat.Mp4)]
    [InlineData(OutputFormat.Dash)]
    public async Task Siblings_CallEncoderTwice_Pass1ThenPass2(OutputFormat format)
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        IEncodingStrategy strategy = Build(format);
        EncodingResult result = await strategy.EncodeAsync(
            BuildRequest(format),
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(result.Success);
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
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

    [Theory]
    [InlineData(OutputFormat.Mp4)]
    [InlineData(OutputFormat.Dash)]
    public async Task Siblings_SavesCheckpointBetweenPasses(OutputFormat format)
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        IEncodingStrategy strategy = Build(format);
        await strategy.EncodeAsync(BuildRequest(format), null, CancellationToken.None);

        _checkpointStore.Verify(
            s =>
                s.SaveAsync(
                    It.Is<JobCheckpoint>(c => c.Pass1Completed && c.EncodeMode == "TwoPass"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
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
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.FromSeconds(1),
                    Error: null,
                    Metrics: new(1024, 2.0, 24.0, "libx264", null)
                )
            );
    }

    private IEncodingStrategy Build(OutputFormat format) =>
        format switch
        {
            OutputFormat.Mp4 => new Mp4TwoPassStrategy(
                _encoder.Object,
                _checkpointStore.Object,
                NullLogger<Mp4TwoPassStrategy>.Instance,
                TestStorageFactory.CreateLocal()
            ),
            OutputFormat.Dash => new DashTwoPassStrategy(
                _encoder.Object,
                _checkpointStore.Object,
                NullLogger<DashTwoPassStrategy>.Instance,
                TestStorageFactory.CreateLocal()
            ),
            _ => throw new ArgumentException($"No 2-pass sibling for {format}"),
        };

    private static Container ToContainer(OutputFormat format) =>
        format switch
        {
            OutputFormat.Hls => Container.HlsTs,
            OutputFormat.Dash => Container.Dash,
            OutputFormat.Mp4 => Container.Mp4,
            OutputFormat.Mkv => Container.Mkv,
            _ => Container.HlsTs,
        };

    private EncodingRequest BuildRequest(OutputFormat format) =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: $"{format} 2-pass",
                Container: ToContainer(format),
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.TwoPass
            )
        );
}
