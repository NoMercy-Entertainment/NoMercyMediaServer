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
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Orchestration;

/// <summary>
/// Verifies that <see cref="EncodingOrchestrator"/> routes staging (AcquireLocalPathAsync)
/// through the request's <see cref="EncodingRequest.SourceStorage"/> and publishing
/// through <see cref="EncodingRequest.DestinationStorage"/> when the two storages differ.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class StorageRoutingTests
{
    private readonly Mock<IStrategyResolver> _resolver = new();
    private readonly Mock<IEncoder> _encoder = new();

    private static Mock<IStorage> BuildStorageMock(string stagingPath)
    {
        Mock<IStorage> mock = new();
        mock.Setup(expression: s => s.Driver).Returns(value: new LocalStorageDriver());
        mock.Setup(expression: s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: (string _, CancellationToken _) => new(path: stagingPath));
        return mock;
    }

    private Mock<IEncodingStrategy> BuildSuccessStrategy()
    {
        Mock<IEncodingStrategy> strategy = new();
        strategy.Setup(expression: s => s.Format).Returns(value: OutputFormat.Mp4);
        strategy.Setup(expression: s => s.EncodeMode).Returns(value: EncodeMode.SinglePass);
        strategy
            .Setup(expression: s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out/file.mp4",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "libx264", GpuUsed: null)
                )
            );

        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Mp4, EncodeMode.SinglePass))
            .Returns(value: strategy.Object);

        return strategy;
    }

    [Fact]
    public async Task EncodeAsync_UsesSeparateSourceStorageForStaging()
    {
        Mock<IStorage> sourceStorage = BuildStorageMock(stagingPath: "/tmp/staged-input.mkv");
        Mock<IStorage> destStorage = BuildStorageMock(stagingPath: "/tmp/staged-output");
        destStorage.Setup(expression: s => s.Driver).Returns(value: new LocalStorageDriver());

        BuildSuccessStrategy();

        EncodingRequest request = new(
            InputPath: "remote/show/episode.mkv",
            OutputDirectory: "show/season01",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "MP4 720p",
                Container: Container.Mp4,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.SinglePass
            ),
            SourceStorage: sourceStorage.Object,
            DestinationStorage: destStorage.Object
        );

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: sourceStorage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request: request);

        sourceStorage.Verify(
            expression: s => s.AcquireLocalPathAsync("remote/show/episode.mkv", It.IsAny<CancellationToken>()),
            times: Times.Once,
            failMessage: "source staging must use SourceStorage"
        );

        destStorage.Verify(
            expression: s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Never,
            failMessage: "destination storage must not be used for staging"
        );
    }

    [Fact]
    public async Task EncodeAsync_NullSourceStorage_FallsBackToDiStorage()
    {
        Mock<IStorage> diStorage = BuildStorageMock(stagingPath: "/tmp/staged.mkv");
        diStorage.Setup(expression: s => s.Driver).Returns(value: new LocalStorageDriver());

        BuildSuccessStrategy();

        EncodingRequest request = new(
            InputPath: "local/show/episode.mkv",
            OutputDirectory: "show/season01",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "MP4 720p",
                Container: Container.Mp4,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.SinglePass
            ),
            SourceStorage: null,
            DestinationStorage: null
        );

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: diStorage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request: request);

        diStorage.Verify(
            expression: s => s.AcquireLocalPathAsync("local/show/episode.mkv", It.IsAny<CancellationToken>()),
            times: Times.Once,
            failMessage: "when SourceStorage is null the DI singleton is used for staging"
        );
    }

    [Fact]
    public async Task EncodeAsync_SameStorageForSourceAndDest_OnlyOneAcquireCall()
    {
        Mock<IStorage> sharedStorage = BuildStorageMock(stagingPath: "/tmp/staged.mkv");
        sharedStorage.Setup(expression: s => s.Driver).Returns(value: new LocalStorageDriver());

        BuildSuccessStrategy();

        EncodingRequest request = new(
            InputPath: "local/movie/film.mkv",
            OutputDirectory: "movie/Film (2020)",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "MP4 1080p",
                Container: Container.Mp4,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.SinglePass
            ),
            SourceStorage: sharedStorage.Object,
            DestinationStorage: sharedStorage.Object
        );

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: sharedStorage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request: request);

        sharedStorage.Verify(
            expression: s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Once,
            failMessage: "same-backend encode: exactly one AcquireLocalPathAsync call for the source"
        );
    }
}
