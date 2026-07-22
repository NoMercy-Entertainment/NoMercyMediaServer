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

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Audio;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Storage;
using NoMercy.Tests.OpticalMedia.Infrastructure;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// Testable subclass that short-circuits the MediaContext DB fetch, mirroring
/// the pattern in DiscRipJobEncodeDispatchTests (file-scoped there).
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
/// REQUIREMENT: for a CD rip that both moves its output into the library
/// (<see cref="RipMode.RipAndEncode"/> with a resolved target folder/library)
/// AND carries an identified MusicBrainz release
/// (<see cref="RipRequest.MetadataId"/> set), <see cref="DiscRipJob"/> must
/// re-fetch the full release via <see cref="MusicBrainzReleaseClient"/>,
/// match each ripped track to its MusicBrainz recording by disc position, and
/// tag every successfully-ripped FLAC via <see cref="IAudioMetadataWriter"/>
/// before the files are copied into the library folder — using the release's
/// Cover Art Archive front flag to populate cover art, and never letting a
/// release-not-found or per-track tag-write failure crash the job.
/// </summary>
[Collection(name: "HttpClientProvider")]
[Trait(name: "Category", value: "Unit")]
public sealed class DiscRipJobCdTaggingTests : ProviderHttpHarness
{
    private static readonly Ulid KnownFolderId = Ulid.NewUlid();
    private static readonly Ulid KnownLibraryId = Ulid.NewUlid();

    public DiscRipJobCdTaggingTests()
        : base(httpClientNames: NoMercy.Providers.Helpers.HttpClientNames.MusicBrainz) { }

    private static RipRequest MakeCdRequest(string metadataId, int[] titleIndices) =>
        new(
            DrivePath: "/dev/sr0",
            SelectedTitleIndices: titleIndices,
            MetadataId: metadataId,
            Custom: null,
            LibraryId: KnownLibraryId,
            FolderId: KnownFolderId,
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            Mode: RipMode.RipAndEncode,
            DiscType: OpticalDiscType.Cd
        );

    private static Folder MakeFolder() =>
        new()
        {
            Id = KnownFolderId,
            Path = "/media/music",
            EncodingPresetFolders = [],
        };

    private static Library MakeLibrary() =>
        new()
        {
            Id = KnownLibraryId,
            Title = "Music",
            Type = "music",
            FolderLibraries = [],
        };

    private static async Task<DiscRipResult[]> MakeSuccessResultsWithRealFilesAsync(
        int[] trackIndices
    )
    {
        List<DiscRipResult> results = [];
        foreach (int i in trackIndices)
        {
            string path = Path.Combine(path1: Path.GetTempPath(), path2: $"cdtag_{Guid.NewGuid():N}_{i:D2}.flac");
            await File.WriteAllBytesAsync(path: path, bytes: []);
            results.Add(
                item: new(
                    TitleIndex: i,
                    OutputPath: path,
                    Success: true,
                    Duration: TimeSpan.FromMinutes(minutes: 3),
                    OutputSizeBytes: 20_000_000,
                    Error: null
                )
            );
        }
        return results.ToArray();
    }

    private static Mock<IStorage> MakeStorageMock(string hostPath)
    {
        Mock<IStorage> storage = new();
        storage
            .Setup(expression: s => s.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: rel => hostPath + "/" + rel.TrimStart(trimChar: '/'));
        storage
            .Setup(expression: s => s.CreateDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);
        storage
            .Setup(expression: s =>
                s.OpenWriteAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Stream.Null);
        return storage;
    }

    private static DiscRipJob BuildJob(
        RipRequest request,
        IDiscRipper ripper,
        IAudioMetadataWriter tagWriter,
        IStorage folderStorage
    )
    {
        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: folderStorage);

        TestableDiscRipJob job = new(folder: MakeFolder(), library: MakeLibrary())
        {
            Request = request,
            OutputDir = Path.GetTempPath(),
            TargetFolderId = KnownFolderId,
            TargetLibraryId = KnownLibraryId,
            TargetLibraryType = "music",
            DiscRipper = ripper,
            IdentificationService = new(identifiers: [], logger: NullLogger<DiscIdentificationService>.Instance),
            StorageFactory = factoryMock.Object,
            StorageDriver = Mock.Of<IStorageDriver>(),
            DriveLockRegistry = new(),
            LoggerFactory = NullLoggerFactory.Instance,
            AudioMetadataWriter = tagWriter,
            MusicBrainzReleaseClient = new(),
        };

        return job;
    }

    private static string ReleaseJson(Guid releaseId, bool hasFrontCover) =>
        $$"""
            {
              "id": "{{releaseId}}",
              "title": "Test Album",
              "artist-credit": [ { "name": "Test Artist", "joinphrase": "" } ],
              "date": "2015-06-01",
              "genres": [ { "name": "Rock" } ],
              "media": [
                {
                  "track-count": 2,
                  "tracks": [
                    {
                      "position": 1,
                      "id": "{{Guid.NewGuid()}}",
                      "title": "First Track",
                      "artist-credit": [ { "name": "Track Artist", "joinphrase": "" } ],
                      "recording": { "id": "{{Guid.NewGuid()}}", "title": "First Track" }
                    },
                    {
                      "position": 2,
                      "id": "{{Guid.NewGuid()}}",
                      "title": "Second Track",
                      "artist-credit": [ { "name": "Track Artist", "joinphrase": "" } ],
                      "recording": { "id": "{{Guid.NewGuid()}}", "title": "Second Track" }
                    }
                  ]
                }
              ],
              "cover-art-archive": { "front": {{(hasFrontCover ? "true" : "false")}} }
            }
            """;

    [Fact]
    public async Task Handle_CdWithMetadataId_TagsEachRippedTrackWithMatchedRecording()
    {
        Guid releaseId = Guid.NewGuid();
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, hasFrontCover: true))
        );

        DiscRipResult[] ripResults = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1, 2]);
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
                .ReturnsAsync(value: ripResults);

            List<AudioMetadata> tagged = [];
            Mock<IAudioMetadataWriter> tagWriterMock = new();
            tagWriterMock
                .Setup(expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<string, AudioMetadata, CancellationToken>(
                    action: (_, meta, _) => tagged.Add(item: meta)
                )
                .Returns(value: Task.CompletedTask);

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: releaseId.ToString(), titleIndices: [1, 2]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            await job.Handle();

            tagged.Should().HaveCount(expected: 2);
            tagged[index: 0].Title.Should().Be(expected: "First Track");
            tagged[index: 0].Artist.Should().Be(expected: "Track Artist");
            tagged[index: 0].AlbumArtist.Should().Be(expected: "Test Artist");
            tagged[index: 0].Album.Should().Be(expected: "Test Album");
            tagged[index: 0].Year.Should().Be(expected: 2015);
            tagged[index: 0].Genre.Should().Be(expected: "Rock");
            tagged[index: 0].CoverArt.Should().NotBeNull();
            tagged[index: 1].Title.Should().Be(expected: "Second Track");
        }
        finally
        {
            foreach (DiscRipResult r in ripResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }

    [Fact]
    public async Task Handle_CdWithMetadataId_NoFrontCover_CoverArtIsNull()
    {
        Guid releaseId = Guid.NewGuid();
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, hasFrontCover: false))
        );

        DiscRipResult[] ripResults = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1]);
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
                .ReturnsAsync(value: ripResults);

            List<AudioMetadata> tagged = [];
            Mock<IAudioMetadataWriter> tagWriterMock = new();
            tagWriterMock
                .Setup(expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<string, AudioMetadata, CancellationToken>(
                    action: (_, meta, _) => tagged.Add(item: meta)
                )
                .Returns(value: Task.CompletedTask);

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: releaseId.ToString(), titleIndices: [1]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            await job.Handle();

            tagged.Should().ContainSingle();
            tagged[index: 0].CoverArt.Should().BeNull();
        }
        finally
        {
            foreach (DiscRipResult r in ripResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }

    [Fact]
    public async Task Handle_CdWithMetadataId_ReleaseNotFound_SkipsTaggingWithoutThrowing()
    {
        string metadataId = Guid.NewGuid().ToString();
        Handler.WhenGet(pathContains: $"release/{metadataId}", responses: MockResponse.Status(status: HttpStatusCode.NotFound));

        DiscRipResult[] ripResults = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1]);
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
                .ReturnsAsync(value: ripResults);

            Mock<IAudioMetadataWriter> tagWriterMock = new();

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: metadataId, titleIndices: [1]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            Exception? ex = await Record.ExceptionAsync(testCode: () => job.Handle());

            ex.Should()
                .BeNull(because: "a not-found release must degrade to untagged FLACs, not crash the job");
            tagWriterMock.Verify(
                expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Never
            );
        }
        finally
        {
            foreach (DiscRipResult r in ripResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }

    [Fact]
    public async Task Handle_CdWithMetadataId_ReleaseFetchThrows_SkipsTaggingWithoutCrashingJob()
    {
        // 500 (not a soft-fail status, not Queue-retried) makes
        // WithAllAppends genuinely throw — unlike the 404 case above, this
        // exercises TagCdTracksAsync's own try/catch around the MusicBrainz
        // release fetch rather than the "release is null" branch below it.
        string metadataId = Guid.NewGuid().ToString();
        Handler.WhenGet(
            pathContains: $"release/{metadataId}",
            responses: MockResponse.Status(status: HttpStatusCode.InternalServerError)
        );

        DiscRipResult[] ripResults = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1]);
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
                .ReturnsAsync(value: ripResults);

            Mock<IAudioMetadataWriter> tagWriterMock = new();

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: metadataId, titleIndices: [1]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            Exception? ex = await Record.ExceptionAsync(testCode: () => job.Handle());

            ex.Should()
                .BeNull(
                    because: "a release-fetch failure must degrade to untagged FLACs, not crash the job"
                );
            tagWriterMock.Verify(
                expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Never
            );
        }
        finally
        {
            foreach (DiscRipResult r in ripResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }

    [Fact]
    public async Task Handle_CdWithMetadataId_TrackNotMatchedInMedium_FallsBackToTrackNumberTitle()
    {
        Guid releaseId = Guid.NewGuid();
        // Only 2 tracks in the release medium, but the disc rip has 3 —
        // the position lookup in TagCdTracksAsync should fail to find track
        // 3, falling back to "Track 03" / the album artist credit.
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, hasFrontCover: false))
        );

        DiscRipResult[] ripResults = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1, 2, 3]);
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
                .ReturnsAsync(value: ripResults);

            List<AudioMetadata> tagged = [];
            Mock<IAudioMetadataWriter> tagWriterMock = new();
            tagWriterMock
                .Setup(expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<string, AudioMetadata, CancellationToken>(
                    action: (_, meta, _) => tagged.Add(item: meta)
                )
                .Returns(value: Task.CompletedTask);

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: releaseId.ToString(), titleIndices: [1, 2, 3]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            await job.Handle();

            tagged.Should().HaveCount(expected: 3);
            tagged[index: 2].Title.Should().Be(expected: "Track 03");
            tagged[index: 2].Artist.Should().Be(expected: "Test Artist", because: "falls back to the album artist credit");
        }
        finally
        {
            foreach (DiscRipResult r in ripResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }

    [Fact]
    public async Task Handle_CdWithMetadataId_TagWriterThrowsForOneTrack_ContinuesWithRemainingTracks()
    {
        Guid releaseId = Guid.NewGuid();
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, hasFrontCover: false))
        );

        DiscRipResult[] ripResults = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1, 2]);
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
                .ReturnsAsync(value: ripResults);

            Mock<IAudioMetadataWriter> tagWriterMock = new();
            tagWriterMock
                .SetupSequence(expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(exception: new InvalidOperationException(message: "disk full"))
                .Returns(value: Task.CompletedTask);

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: releaseId.ToString(), titleIndices: [1, 2]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            Exception? ex = await Record.ExceptionAsync(testCode: () => job.Handle());

            ex.Should().BeNull(because: "one track's tag failure must not stop the rest from being tagged");
            tagWriterMock.Verify(
                expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Exactly(callCount: 2)
            );
        }
        finally
        {
            foreach (DiscRipResult r in ripResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }

    [Fact]
    public async Task Handle_CdWithMetadataId_OnlyTagsSuccessfulRips()
    {
        Guid releaseId = Guid.NewGuid();
        Handler.WhenGet(
            pathContains: $"release/{releaseId}",
            responses: MockResponse.Json(status: HttpStatusCode.OK, body: ReleaseJson(releaseId: releaseId, hasFrontCover: false))
        );

        DiscRipResult[] successFile = await MakeSuccessResultsWithRealFilesAsync(trackIndices: [1]);
        DiscRipResult[] mixedResults =
        [
            successFile[0],
            new(
                TitleIndex: 2,
                OutputPath: Path.Combine(
                    path1: Path.GetTempPath(),
                    path2: $"cdtag_missing_{Guid.NewGuid():N}.flac"
                ),
                Success: false,
                Duration: TimeSpan.Zero,
                OutputSizeBytes: 0,
                Error: "read error"
            ),
        ];
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
                .ReturnsAsync(value: mixedResults);

            Mock<IAudioMetadataWriter> tagWriterMock = new();

            Mock<IEventBus> busMock = new();
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<DriveStateChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            busMock
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<NoMercy.Events.FileWatcher.FileCreatedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(value: Task.CompletedTask);
            EventBusProvider.Configure(eventBus: busMock.Object);

            DiscRipJob job = BuildJob(
                request: MakeCdRequest(metadataId: releaseId.ToString(), titleIndices: [1, 2]),
                ripper: ripperMock.Object,
                tagWriter: tagWriterMock.Object,
                folderStorage: MakeStorageMock(hostPath: "/media/music").Object
            );

            await job.Handle();

            tagWriterMock.Verify(
                expression: w =>
                    w.WriteTagsAsync(
                        It.Is<string>(p => p.Contains("cdtag_missing")),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Never,
                failMessage: "the failed track must never be tagged"
            );
            tagWriterMock.Verify(
                expression: w =>
                    w.WriteTagsAsync(
                        It.IsAny<string>(),
                        It.IsAny<AudioMetadata>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Once
            );
        }
        finally
        {
            foreach (DiscRipResult r in mixedResults)
                if (File.Exists(path: r.OutputPath))
                    File.Delete(path: r.OutputPath);
        }
    }
}
