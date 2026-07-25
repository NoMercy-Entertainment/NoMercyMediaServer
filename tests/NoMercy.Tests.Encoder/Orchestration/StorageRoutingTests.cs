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
[Trait("Category", "Unit")]
public class StorageRoutingTests
{
    private readonly Mock<IStrategyResolver> _resolver = new();
    private readonly Mock<IEncoder> _encoder = new();

    private static Mock<IStorage> BuildStorageMock(string stagingPath)
    {
        Mock<IStorage> mock = new();
        mock.Setup(s => s.Driver).Returns(new LocalStorageDriver());
        mock.Setup(s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, CancellationToken _) => new(stagingPath));
        return mock;
    }

    private Mock<IEncodingStrategy> BuildSuccessStrategy()
    {
        Mock<IEncodingStrategy> strategy = new();
        strategy.Setup(s => s.Format).Returns(OutputFormat.Mp4);
        strategy.Setup(s => s.EncodeMode).Returns(EncodeMode.SinglePass);
        strategy
            .Setup(s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    true,
                    "/out/file.mp4",
                    TimeSpan.Zero,
                    null,
                    new(0, 0, 0, "libx264", null)
                )
            );

        _resolver
            .Setup(r => r.Resolve(OutputFormat.Mp4, EncodeMode.SinglePass))
            .Returns(strategy.Object);

        return strategy;
    }

    [Fact]
    public async Task EncodeAsync_UsesSeparateSourceStorageForStaging()
    {
        Mock<IStorage> sourceStorage = BuildStorageMock("/tmp/staged-input.mkv");
        Mock<IStorage> destStorage = BuildStorageMock("/tmp/staged-output");
        destStorage.Setup(s => s.Driver).Returns(new LocalStorageDriver());

        BuildSuccessStrategy();

        EncodingRequest request = new(
            "remote/show/episode.mkv",
            OutputDirectory: "show/season01",
            Profile: new(
                Ulid.NewUlid(),
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
            _resolver.Object,
            sourceStorage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request);

        sourceStorage.Verify(
            s => s.AcquireLocalPathAsync("remote/show/episode.mkv", It.IsAny<CancellationToken>()),
            Times.Once,
            "source staging must use SourceStorage"
        );

        destStorage.Verify(
            s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "destination storage must not be used for staging"
        );
    }

    [Fact]
    public async Task EncodeAsync_NullSourceStorage_FallsBackToDiStorage()
    {
        Mock<IStorage> diStorage = BuildStorageMock("/tmp/staged.mkv");
        diStorage.Setup(s => s.Driver).Returns(new LocalStorageDriver());

        BuildSuccessStrategy();

        EncodingRequest request = new(
            "local/show/episode.mkv",
            OutputDirectory: "show/season01",
            Profile: new(
                Ulid.NewUlid(),
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
            _resolver.Object,
            diStorage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request);

        diStorage.Verify(
            s => s.AcquireLocalPathAsync("local/show/episode.mkv", It.IsAny<CancellationToken>()),
            Times.Once,
            "when SourceStorage is null the DI singleton is used for staging"
        );
    }

    [Fact]
    public async Task EncodeAsync_SameStorageForSourceAndDest_OnlyOneAcquireCall()
    {
        Mock<IStorage> sharedStorage = BuildStorageMock("/tmp/staged.mkv");
        sharedStorage.Setup(s => s.Driver).Returns(new LocalStorageDriver());

        BuildSuccessStrategy();

        EncodingRequest request = new(
            "local/movie/film.mkv",
            OutputDirectory: "movie/Film (2020)",
            Profile: new(
                Ulid.NewUlid(),
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
            _resolver.Object,
            sharedStorage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request);

        sharedStorage.Verify(
            s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "same-backend encode: exactly one AcquireLocalPathAsync call for the source"
        );
    }
}
