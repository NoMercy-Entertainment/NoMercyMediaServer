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
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"TwoPassSiblings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    public static TheoryData<IEncodingStrategy, OutputFormat> SiblingsWithFormat()
    {
        Mock<IEncoder> encoder = new();
        Mock<ICheckpointStore> store = new();
        return new()
        {
            {
                new HlsTwoPassStrategy(
                    encoder: encoder.Object,
                    checkpointStore: store.Object,
                    logger: NullLogger<HlsTwoPassStrategy>.Instance,
                    storage: TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Hls
            },
            {
                new Mp4TwoPassStrategy(
                    encoder: encoder.Object,
                    checkpointStore: store.Object,
                    logger: NullLogger<Mp4TwoPassStrategy>.Instance,
                    storage: TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Mp4
            },
            {
                new DashTwoPassStrategy(
                    encoder: encoder.Object,
                    checkpointStore: store.Object,
                    logger: NullLogger<DashTwoPassStrategy>.Instance,
                    storage: TestStorageFactory.CreateLocal()
                ),
                OutputFormat.Dash
            },
        };
    }

    [Theory]
    [MemberData(memberName: nameof(SiblingsWithFormat))]
    public void Siblings_ReportCorrectFormatAndTwoPassMode(
        IEncodingStrategy strategy,
        OutputFormat expectedFormat
    )
    {
        Assert.Equal(expected: expectedFormat, actual: strategy.Format);
        Assert.Equal(expected: EncodeMode.TwoPass, actual: strategy.EncodeMode);
    }

    [Theory]
    [InlineData(data: OutputFormat.Mp4)]
    [InlineData(data: OutputFormat.Dash)]
    public async Task Siblings_CallEncoderTwice_Pass1ThenPass2(OutputFormat format)
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        IEncodingStrategy strategy = Build(format: format);
        EncodingResult result = await strategy.EncodeAsync(
            request: BuildRequest(format: format),
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(condition: result.Success);
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
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

    [Theory]
    [InlineData(data: OutputFormat.Mp4)]
    [InlineData(data: OutputFormat.Dash)]
    public async Task Siblings_SavesCheckpointBetweenPasses(OutputFormat format)
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        IEncodingStrategy strategy = Build(format: format);
        await strategy.EncodeAsync(request: BuildRequest(format: format), progress: null, ct: CancellationToken.None);

        _checkpointStore.Verify(
            expression: s =>
                s.SaveAsync(
                    It.Is<JobCheckpoint>(c => c.Pass1Completed && c.EncodeMode == "TwoPass"),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
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
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    Error: null,
                    Metrics: new(OutputSizeBytes: 1024, AverageSpeed: 2.0, AverageFps: 24.0, EncoderUsed: "libx264", GpuUsed: null)
                )
            );
    }

    private IEncodingStrategy Build(OutputFormat format) =>
        format switch
        {
            OutputFormat.Mp4 => new Mp4TwoPassStrategy(
                encoder: _encoder.Object,
                checkpointStore: _checkpointStore.Object,
                logger: NullLogger<Mp4TwoPassStrategy>.Instance,
                storage: TestStorageFactory.CreateLocal()
            ),
            OutputFormat.Dash => new DashTwoPassStrategy(
                encoder: _encoder.Object,
                checkpointStore: _checkpointStore.Object,
                logger: NullLogger<DashTwoPassStrategy>.Instance,
                storage: TestStorageFactory.CreateLocal()
            ),
            _ => throw new ArgumentException(message: $"No 2-pass sibling for {format}"),
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
                Container: ToContainer(format: format),
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.TwoPass
            )
        );
}
