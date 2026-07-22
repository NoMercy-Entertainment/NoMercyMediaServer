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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;
using OpticalMediaType = NoMercy.OpticalMedia.Metadata.MediaType;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// Testable subclass that short-circuits the MediaContext DB fetch by
/// returning pre-seeded objects. This avoids a live SQLite file while
/// still exercising the full dispatch logic.
/// </summary>
file sealed class TestableDiscRipJob(Folder folder, Library library) : DiscRipJob
{
    protected override Task<(Folder? Folder, Library? Library)> FetchTargetsAsync(
        Ulid folderId,
        Ulid libraryId,
        CancellationToken cancellationToken
    ) => Task.FromResult<(Folder?, Library?)>(result: (folder, library));
}

/// <summary>
/// Tests for the RipAndEncode video dispatch path added in Wave 3.
/// The static <see cref="QueueRunner"/> is bypassed by injecting
/// <see cref="IJobDispatcher"/> directly via <see cref="DiscRipJob.JobDispatcher"/>.
/// The MediaContext DB fetch is bypassed via <see cref="TestableDiscRipJob"/>.
/// </summary>
[Collection(name: "EventBusProvider")]
[Trait(name: "Category", value: "Unit")]
public class DiscRipJobEncodeDispatchTests
{
    private static readonly Ulid KnownFolderId = Ulid.NewUlid();
    private static readonly Ulid KnownLibraryId = Ulid.NewUlid();
    private static readonly Ulid KnownPresetId = Ulid.NewUlid();

    private static RipRequest MakeVideoRequest(
        OpticalDiscType discType = OpticalDiscType.Dvd,
        string? encodingProfileId = null,
        RipMode mode = RipMode.RipAndEncode
    ) =>
        new(
            DrivePath: "D:\\",
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: new(
                Title: "Test Film",
                Year: 2024,
                Type: OpticalMediaType.Movie,
                PosterUrl: null
            ),
            LibraryId: KnownLibraryId,
            FolderId: KnownFolderId,
            EncodingProfileId: encodingProfileId,
            AudioTracks: [],
            Subtitles: [],
            Mode: mode,
            DiscType: discType
        );

    private static EncodingPresetFolder MakePresetLink(Ulid presetId) =>
        new()
        {
            PresetId = presetId,
            FolderId = KnownFolderId,
            IsDefault = false,
            Preset = new()
            {
                Id = presetId,
                Name = "Test Preset",
                ProfileJson = "{}",
            },
        };

    private static Folder MakeFolder(IEnumerable<EncodingPresetFolder>? presetLinks = null) =>
        new()
        {
            Id = KnownFolderId,
            Path = "/media/movies",
            EncodingPresetFolders = (presetLinks ?? []).ToList(),
        };

    private static Library MakeLibrary() =>
        new()
        {
            Id = KnownLibraryId,
            Title = "Movies",
            Type = "movie",
            FolderLibraries = [],
        };

    private static DiscRipResult MakeRipResult(string outputPath = "/tmp/title_01.mkv") =>
        new(
            TitleIndex: 1,
            OutputPath: outputPath,
            Success: true,
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OutputSizeBytes: 1_000_000,
            Error: null
        );

    private static Mock<IStorage> MakeStorageMock(string hostPath)
    {
        Mock<IStorage> storageMock = new();
        storageMock
            .Setup(expression: s => s.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: relative => hostPath + "/" + relative.TrimStart(trimChar: '/'));
        storageMock
            .Setup(expression: s => s.CreateDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);
        storageMock
            .Setup(expression: s =>
                s.OpenWriteAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Stream.Null);
        return storageMock;
    }

    private static DiscRipJob BuildJob(
        RipRequest request,
        IDiscRipper ripper,
        IJobDispatcher? jobDispatcher,
        IStorage folderStorage,
        Folder folder,
        Library library
    )
    {
        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: folderStorage);

        TestableDiscRipJob job = new(folder: folder, library: library)
        {
            Request = request,
            OutputDir = Path.GetTempPath(),
            TargetFolderId = folder.Id,
            TargetLibraryId = library.Id,
            TargetLibraryType = library.Type,
            DiscRipper = ripper,
            IdentificationService = new(
                identifiers: [],
                logger: NullLogger<DiscIdentificationService>.Instance
            ),
            StorageFactory = factoryMock.Object,
            StorageDriver = Mock.Of<IStorageDriver>(),
            DriveLockRegistry = new(),
            LoggerFactory = NullLoggerFactory.Instance,
            JobDispatcher = jobDispatcher,
        };

        return job;
    }

    // ── Dispatch with explicit matching preset ────────────────────────────

    [Fact]
    public async Task Handle_VideoRipAndEncode_ExplicitMatchingPreset_DispatchesVideoEncodeJobWithPresetId()
    {
        string tempFile = Path.Combine(path1: Path.GetTempPath(), path2: "title_01.mkv");
        await File.WriteAllBytesAsync(path: tempFile, bytes: []);

        try
        {
            Mock<IDiscRipper> ripperMock = new();
            ripperMock
                .Setup(expression: r =>
                    r.RipAsync(
                        It.IsAny<RipRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: [MakeRipResult(outputPath: tempFile)]);

            List<VideoEncodeJob> dispatched = [];
            Mock<IJobDispatcher> dispatcherMock = new();
            dispatcherMock
                .Setup(expression: d =>
                    d.Dispatch(It.IsAny<IShouldQueue>(), It.IsAny<string>(), It.IsAny<int>())
                )
                .Callback<IShouldQueue, string, int>(
                    action: (job, _, _) =>
                    {
                        if (job is VideoEncodeJob encodeJob)
                            dispatched.Add(item: encodeJob);
                    }
                );

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            Folder folder = MakeFolder(presetLinks: [MakePresetLink(presetId: KnownPresetId)]);
            Library library = MakeLibrary();
            Mock<IStorage> storageMock = MakeStorageMock(hostPath: "/media/movies");

            RipRequest request = MakeVideoRequest(
                discType: OpticalDiscType.Dvd,
                encodingProfileId: KnownPresetId.ToString()
            );

            DiscRipJob job = BuildJob(
                request: request,
                ripper: ripperMock.Object,
                jobDispatcher: dispatcherMock.Object,
                folderStorage: storageMock.Object,
                folder: folder,
                library: library
            );

            await job.Handle();

            dispatched.Should().HaveCount(expected: 1, because: "one VideoEncodeJob must be dispatched per title");

            VideoEncodeJob encodeJob = dispatched[index: 0];
            encodeJob.LibraryId.Should().Be(expected: KnownLibraryId);
            encodeJob.FolderId.Should().Be(expected: KnownFolderId);
            encodeJob.InputFile.Should().NotBeNullOrEmpty();
            encodeJob.PresetId.Should().Be(expected: KnownPresetId);
        }
        finally
        {
            if (File.Exists(path: tempFile))
                File.Delete(path: tempFile);
        }
    }

    // ── Dispatch without matching preset (falls back to null) ────────────

    [Fact]
    public async Task Handle_VideoRipAndEncode_EncodingProfileIdNotInFolder_DispatchesWithNullPresetId()
    {
        string tempFile = Path.Combine(path1: Path.GetTempPath(), path2: "title_02.mkv");
        await File.WriteAllBytesAsync(path: tempFile, bytes: []);

        try
        {
            Mock<IDiscRipper> ripperMock = new();
            ripperMock
                .Setup(expression: r =>
                    r.RipAsync(
                        It.IsAny<RipRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: [MakeRipResult(outputPath: tempFile)]);

            List<VideoEncodeJob> dispatched = [];
            Mock<IJobDispatcher> dispatcherMock = new();
            dispatcherMock
                .Setup(expression: d =>
                    d.Dispatch(It.IsAny<IShouldQueue>(), It.IsAny<string>(), It.IsAny<int>())
                )
                .Callback<IShouldQueue, string, int>(
                    action: (job, _, _) =>
                    {
                        if (job is VideoEncodeJob encodeJob)
                            dispatched.Add(item: encodeJob);
                    }
                );

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            // Folder has KnownPresetId but request asks for a different one
            Ulid differentPresetId = Ulid.NewUlid();
            Folder folder = MakeFolder(presetLinks: [MakePresetLink(presetId: KnownPresetId)]);
            Library library = MakeLibrary();
            Mock<IStorage> storageMock = MakeStorageMock(hostPath: "/media/movies");

            RipRequest request = MakeVideoRequest(
                discType: OpticalDiscType.BluRay,
                encodingProfileId: differentPresetId.ToString()
            );

            DiscRipJob job = BuildJob(
                request: request,
                ripper: ripperMock.Object,
                jobDispatcher: dispatcherMock.Object,
                folderStorage: storageMock.Object,
                folder: folder,
                library: library
            );

            await job.Handle();

            dispatched.Should().HaveCount(expected: 1);
            dispatched[index: 0].PresetId.Should().BeNull(because: "unmatched profile id must fall back to null");
        }
        finally
        {
            if (File.Exists(path: tempFile))
                File.Delete(path: tempFile);
        }
    }

    // ── Dispatch with no profile id (folder default) ──────────────────────

    [Fact]
    public async Task Handle_VideoRipAndEncode_NullEncodingProfileId_DispatchesWithNullPresetId()
    {
        string tempFile = Path.Combine(path1: Path.GetTempPath(), path2: "title_03.mkv");
        await File.WriteAllBytesAsync(path: tempFile, bytes: []);

        try
        {
            Mock<IDiscRipper> ripperMock = new();
            ripperMock
                .Setup(expression: r =>
                    r.RipAsync(
                        It.IsAny<RipRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: [MakeRipResult(outputPath: tempFile)]);

            List<VideoEncodeJob> dispatched = [];
            Mock<IJobDispatcher> dispatcherMock = new();
            dispatcherMock
                .Setup(expression: d =>
                    d.Dispatch(It.IsAny<IShouldQueue>(), It.IsAny<string>(), It.IsAny<int>())
                )
                .Callback<IShouldQueue, string, int>(
                    action: (job, _, _) =>
                    {
                        if (job is VideoEncodeJob encodeJob)
                            dispatched.Add(item: encodeJob);
                    }
                );

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            Folder folder = MakeFolder(presetLinks: [MakePresetLink(presetId: KnownPresetId)]);
            Library library = MakeLibrary();
            Mock<IStorage> storageMock = MakeStorageMock(hostPath: "/media/movies");

            RipRequest request = MakeVideoRequest(
                discType: OpticalDiscType.Dvd,
                encodingProfileId: null
            );

            DiscRipJob job = BuildJob(
                request: request,
                ripper: ripperMock.Object,
                jobDispatcher: dispatcherMock.Object,
                folderStorage: storageMock.Object,
                folder: folder,
                library: library
            );

            await job.Handle();

            dispatched.Should().HaveCount(expected: 1);
            dispatched[index: 0].PresetId.Should().BeNull(because: "absent profile id must produce null PresetId");
        }
        finally
        {
            if (File.Exists(path: tempFile))
                File.Delete(path: tempFile);
        }
    }

    // ── CD rip must NOT dispatch VideoEncodeJob ───────────────────────────

    [Fact]
    public async Task Handle_CdRipAndEncode_DoesNotDispatchVideoEncodeJob()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);

        List<IShouldQueue> dispatched = [];
        Mock<IJobDispatcher> dispatcherMock = new();
        dispatcherMock
            .Setup(expression: d => d.Dispatch(It.IsAny<IShouldQueue>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<IShouldQueue, string, int>(action: (job, _, _) => dispatched.Add(item: job));

        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        Folder folder = MakeFolder();
        Library library = MakeLibrary();
        Mock<IStorage> storageMock = MakeStorageMock(hostPath: "/media/music");

        RipRequest request = MakeVideoRequest(discType: OpticalDiscType.Cd);

        DiscRipJob job = BuildJob(
            request: request,
            ripper: ripperMock.Object,
            jobDispatcher: dispatcherMock.Object,
            folderStorage: storageMock.Object,
            folder: folder,
            library: library
        );

        await job.Handle();

        dispatched
            .Should()
            .NotContain(predicate: j => j is VideoEncodeJob, because: "CD rips must not trigger video encode");
    }

    // ── Dispatcher null falls back to FileCreatedEvent ───────────────────

    [Fact]
    public async Task Handle_VideoRipAndEncode_NullDispatcher_FallsBackToFileCreatedEvent()
    {
        string tempFile = Path.Combine(path1: Path.GetTempPath(), path2: "title_04.mkv");
        await File.WriteAllBytesAsync(path: tempFile, bytes: []);

        try
        {
            Mock<IDiscRipper> ripperMock = new();
            ripperMock
                .Setup(expression: r =>
                    r.RipAsync(
                        It.IsAny<RipRequest>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: [MakeRipResult(outputPath: tempFile)]);

            List<object> publishedEvents = [];
            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<DriveStateChangedEvent, CancellationToken>(
                    action: (evt, _) => publishedEvents.Add(item: evt)
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<NoMercy.Events.FileWatcher.FileCreatedEvent, CancellationToken>(
                    action: (evt, _) => publishedEvents.Add(item: evt)
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            Folder folder = MakeFolder(presetLinks: [MakePresetLink(presetId: KnownPresetId)]);
            Library library = MakeLibrary();
            Mock<IStorage> storageMock = MakeStorageMock(hostPath: "/media/movies");

            RipRequest request = MakeVideoRequest(discType: OpticalDiscType.Dvd);

            DiscRipJob job = BuildJob(
                request: request,
                ripper: ripperMock.Object,
                jobDispatcher: null,
                folderStorage: storageMock.Object,
                folder: folder,
                library: library
            );

            await job.Handle();

            publishedEvents
                .OfType<NoMercy.Events.FileWatcher.FileCreatedEvent>()
                .Should()
                .HaveCount(expected: 1, because: "null dispatcher must fall back to FileCreatedEvent");
        }
        finally
        {
            if (File.Exists(path: tempFile))
                File.Delete(path: tempFile);
        }
    }
}
