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

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.Audio;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// Testable subclass that reports whichever (Folder, Library) tuple the test
/// configures — including the null/null "target no longer exists" case that
/// the real DB-backed <see cref="DiscRipJob.FetchTargetsAsync"/> can also
/// return.
/// </summary>
file sealed class ConfigurableTargetsDiscRipJob(Folder? folder, Library? library) : DiscRipJob
{
    protected override Task<(Folder? Folder, Library? Library)> FetchTargetsAsync(
        Ulid folderId,
        Ulid libraryId,
        CancellationToken cancellationToken
    ) => Task.FromResult(result: (folder, library));
}

/// <summary>
/// Covers the small remaining surfaces of <see cref="DiscRipJob"/> not
/// exercised by the auto-apply / CD-tagging / encode-dispatch test files:
/// the fixed queue name/priority, <see cref="DiscRipJob.InjectStorageServices"/>
/// wiring every field from a real <see cref="IServiceProvider"/>,
/// preset-id resolution against a malformed id, the "target folder/library no
/// longer exists" branch, and <c>ResolveHostPath</c>'s fallback when
/// <see cref="IStorage.GetFullPath"/> throws.
/// </summary>
[Collection(name: "EventBusProvider")]
[Trait(name: "Category", value: "Unit")]
public class DiscRipJobMiscTests
{
    [Fact]
    public void QueueName_IsImport()
    {
        DiscRipJob job = new();
        job.QueueName.Should().Be(expected: "import");
    }

    [Fact]
    public void Priority_IsFive()
    {
        DiscRipJob job = new();
        job.Priority.Should().Be(expected: 5);
    }

    [Fact]
    public void InjectStorageServices_ResolvesEveryDependencyFromServiceProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(implementationInstance: NullLoggerFactory.Instance);
        services.AddSingleton(implementationInstance: Mock.Of<IDiscRipper>());
        services.AddSingleton(
            implementationInstance: new DiscIdentificationService(identifiers: [], logger: NullLogger<DiscIdentificationService>.Instance)
        );
        services.AddSingleton(implementationInstance: Mock.Of<IStorageFactory>());
        services.AddSingleton(implementationInstance: Mock.Of<IStorageDriver>());
        services.AddSingleton(implementationInstance: new DriveLockRegistry());
        services.AddSingleton(implementationInstance: Mock.Of<IAudioMetadataWriter>());
        using ServiceProvider provider = services.BuildServiceProvider();

        DiscRipJob job = new();
        job.InjectStorageServices(serviceProvider: provider);

        job.LoggerFactory.Should().BeSameAs(expected: NullLoggerFactory.Instance);
        job.DiscRipper.Should().NotBeNull();
        job.IdentificationService.Should().NotBeNull();
        job.StorageFactory.Should().NotBeNull();
        job.StorageDriver.Should().NotBeNull();
        job.DriveLockRegistry.Should().NotBeNull();
        job.AudioMetadataWriter.Should().NotBeNull();
        job.MusicBrainzReleaseClient.Should()
            .NotBeNull(because: "constructed directly since it isn't DI-registered");
        // QueueRunner.Current is never started by this test process, so the
        // dispatcher falls back to null — this proves that fallback runs
        // rather than throwing when no queue runner is active.
        job.JobDispatcher.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TargetsNoLongerExist_PublishesErrorAndReturns()
    {
        RipRequest request = new(
            DrivePath: "D:\\",
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: new(Title: "Movie", Year: 2024, Type: MediaType.Movie, PosterUrl: null),
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            Mode: RipMode.RipAndEncode,
            DiscType: OpticalDiscType.Dvd
        );

        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value:
            [
                new DiscRipResult(TitleIndex: 1, OutputPath: "/tmp/x.mkv", Success: true, Duration: TimeSpan.FromMinutes(minutes: 10), OutputSizeBytes: 1000, Error: null),
            ]);

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        ConfigurableTargetsDiscRipJob job = new(folder: null, library: null)
        {
            Request = request,
            OutputDir = Path.GetTempPath(),
            TargetFolderId = request.FolderId,
            TargetLibraryId = request.LibraryId,
            TargetLibraryType = "movie",
            DiscRipper = ripperMock.Object,
            IdentificationService = new(identifiers: [], logger: NullLogger<DiscIdentificationService>.Instance),
            StorageFactory = Mock.Of<IStorageFactory>(),
            StorageDriver = Mock.Of<IStorageDriver>(),
            DriveLockRegistry = new(),
            LoggerFactory = NullLoggerFactory.Instance,
        };

        await job.Handle();

        published.Select(selector: e => e.DriveStateData.Method).Should().Contain(expected: "rip_error");
        published
            .Select(selector: e => e.DriveStateData.Message)
            .Should()
            .Contain(predicate: m => m.Contains("no longer exists"));
    }

    [Fact]
    public void PublishProgress_UnrecognizedStatus_FallsBackToRipProgressMethodName()
    {
        // "started"/"complete"/"error"/"pending" are the only statuses any
        // call site in DiscRipJob ever passes — the switch's `_ =>
        // "rip_progress"` default arm is otherwise unreachable from the
        // public API, so it's exercised directly via reflection.
        RipRequest request = new(
            DrivePath: "D:\\",
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: null,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: []
        );
        DiscRipJob job = new(request: request, outputDir: Path.GetTempPath(), targetFolderId: null, targetLibraryId: null, targetLibraryType: null)
        {
            LoggerFactory = NullLoggerFactory.Instance,
        };

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        MethodInfo publishProgress = typeof(DiscRipJob).GetMethod(
            name: "PublishProgress",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        publishProgress.Invoke(obj: job, parameters: ["unknown-status", "some message"]);

        published.Should().ContainSingle();
        published[index: 0].DriveStateData.Method.Should().Be(expected: "rip_progress");
    }

    [Fact]
    public void ResolvePresetId_MalformedUlidString_ReturnsNull()
    {
        MethodInfo resolvePresetId = typeof(DiscRipJob).GetMethod(
            name: "ResolvePresetId",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
        )!;

        object? result = resolvePresetId.Invoke(
            obj: null,
            parameters: ["not-a-valid-ulid", Array.Empty<NoMercy.Database.Models.Media.EncodingPresetFolder>()]
        );

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveHostPath_StorageThrows_FallsBackToRawSubPath()
    {
        Mock<IStorage> storageMock = new();
        storageMock.Setup(expression: s => s.GetFullPath(It.IsAny<string>())).Throws<NotSupportedException>();

        MethodInfo resolveHostPath = typeof(DiscRipJob).GetMethod(
            name: "ResolveHostPath",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
        )!;

        object? result = resolveHostPath.Invoke(obj: null, parameters: [storageMock.Object, "relative/path.mkv"]);

        result.Should().Be(expected: "relative/path.mkv");
    }
}
