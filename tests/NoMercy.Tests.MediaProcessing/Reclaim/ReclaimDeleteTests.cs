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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Reclaim;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.MediaProcessing.Reclaim;

[Trait(name: "Category", value: "Unit")]
public class ReclaimDeleteTests
{
    private static IDbContextFactory<MediaContext> ContextFactory(
        out SqliteConnection connection,
        Action<MediaContext> seed
    )
    {
        SqliteConnection conn = new(connectionString: "DataSource=:memory:");
        conn.Open();
        connection = conn;

        using (SqliteCommand pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: conn)
            .Options;

        using (MediaContext seedContext = new(options: options))
        {
            seedContext.Database.EnsureCreated();
            seed(obj: seedContext);
            seedContext.SaveChanges();
        }

        Mock<IDbContextFactory<MediaContext>> factory = new();
        factory
            .Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));
        factory.Setup(expression: f => f.CreateDbContext()).Returns(valueFunction: () => new(options: options));
        return factory.Object;
    }

    private static T Seed<T>(MediaContext context, T entity)
        where T : class
    {
        context.Add(entity: entity);
        return entity;
    }

    private static VideoFile SeedMovieVideoFile(
        MediaContext context,
        Ulid folderId,
        Ulid driverId,
        string hostFolder,
        string filename,
        int movieId,
        string movieTitle
    )
    {
        Seed(
            context: context,
            entity: new Folder
            {
                Id = folderId,
                Path = hostFolder,
                DriverId = driverId,
            }
        );

        Seed(
            context: context,
            entity: new Movie
            {
                Id = movieId,
                Title = movieTitle,
                TitleSort = movieTitle.ToLowerInvariant(),
                LibraryId = Ulid.NewUlid(),
            }
        );

        return Seed(
            context: context,
            entity: new VideoFile
            {
                Id = Ulid.NewUlid(),
                Filename = filename,
                Folder = hostFolder,
                HostFolder = hostFolder,
                Share = folderId.ToString(),
                MovieId = movieId,
            }
        );
    }

    private static async Task WaitUntilNotScanningAsync(IReclaimScanService service)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(seconds: 5);
        while (service.State == ReclaimScanState.Scanning && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 10);
    }

    private static Mock<IStorage> BuildStorageMock(string hostFolder, List<StorageEntry> entries)
    {
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.List(hostFolder, null, false)).Returns(value: entries);
        storage
            .Setup(expression: s => s.GetName(It.IsAny<string>()))
            .Returns(
                valueFunction: (string path) =>
                {
                    int idx = path.LastIndexOf(value: '/');
                    return idx < 0 ? path : path[(idx + 1)..];
                }
            );
        return storage;
    }

    [Fact]
    public async Task DeleteItemAsync_ReclaimableHlsItem_DeletesExactTargets_UpdatesSnapshot()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Reclaimable Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    driverId: driverId,
                    hostFolder: HostFolder,
                    filename: "/movie.mkv",
                    movieId: 1,
                    movieTitle: "Reclaimable Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> scanEntries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 200_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        List<StorageEntry> freshEntries =
        [
            new(
                Path: $"{HostFolder}/movie.mkv",
                IsDirectory: false,
                SizeBytes: 4_000_000_000,
                LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)
            ),
            new(Path: $"{HostFolder}/movie.NoMercy.m3u8", IsDirectory: false, SizeBytes: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(
                Path: $"{HostFolder}/video_1920x1080_SDR",
                IsDirectory: true,
                SizeBytes: 1_500_000_000,
                LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)
            ),
            new(Path: $"{HostFolder}/audio_eng", IsDirectory: true, SizeBytes: 200_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        Mock<IStorage> storage = BuildStorageMock(hostFolder: HostFolder, entries: freshEntries);
        Mock<IStorageFactory> storageFactory = new();
        storageFactory.Setup(expression: f => f.For(folderId, driverId, string.Empty)).Returns(value: storage.Object);

        ReclaimScanService service = new(
            contextFactory: factory,
            storageFactory: storageFactory.Object,
            configurationStore: new StubConfigurationStore(),
            logger: NullLogger<ReclaimScanService>.Instance,
            listFolderEntriesOverride: (_, _, _) => scanEntries
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(expected: 1);
        ReclaimableItem item = service.Latest.Items[index: 0];

        long freedBytes = await service.DeleteItemAsync(itemId: item.Id, ct: CancellationToken.None);

        freedBytes.Should().Be(expected: 500 + 1_500_000_000 + 200_000_000);

        storage.Verify(
            expression: s => s.DeleteDirectory($"{HostFolder}/video_1920x1080_SDR", true),
            times: Times.Once
        );
        storage.Verify(expression: s => s.DeleteDirectory($"{HostFolder}/audio_eng", true), times: Times.Once);
        storage.Verify(expression: s => s.Delete($"{HostFolder}/movie.NoMercy.m3u8"), times: Times.Once);
        storage.Verify(expression: s => s.Delete($"{HostFolder}/movie.mkv"), times: Times.Never);
        storage.Verify(
            expression: s => s.DeleteDirectory($"{HostFolder}/movie.mkv", It.IsAny<bool>()),
            times: Times.Never
        );

        service.Latest.Items.Should().BeEmpty();
        service.Latest.TotalReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public async Task DeleteItemAsync_TargetNowMatchesServedCopy_RefusesAndDeletesNothing()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Race Condition Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    driverId: driverId,
                    hostFolder: HostFolder,
                    filename: "/movie.mkv",
                    movieId: 2,
                    movieTitle: "Race Condition Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> scanEntries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 200_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        // Strict: any call to the storage factory during the guarded refusal fails the test —
        // the served-copy check must abort before storage is ever touched.
        Mock<IStorageFactory> storageFactory = new(behavior: MockBehavior.Strict);

        ReclaimScanService service = new(
            contextFactory: factory,
            storageFactory: storageFactory.Object,
            configurationStore: new StubConfigurationStore(),
            logger: NullLogger<ReclaimScanService>.Instance,
            listFolderEntriesOverride: (_, _, _) => scanEntries
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(expected: 1);
        ReclaimableItem item = service.Latest.Items[index: 0];
        item.TargetPaths.Should().Contain(expected: $"{HostFolder}/movie.NoMercy.m3u8");

        // Simulate the race: between scan and delete, the served copy pointer moved to the
        // exact file the stale snapshot marked as a reclaim target. Production stores
        // VideoFile.Filename with a leading slash — use that real format, not a bare leaf,
        // so this test cannot stay green against a guard that stopped normalizing it.
        await using (
            MediaContext mutateContext = await factory.CreateDbContextAsync(cancellationToken: CancellationToken.None)
        )
        {
            VideoFile row = await mutateContext.VideoFiles.SingleAsync(predicate: v =>
                v.HostFolder == HostFolder
            );
            row.Filename = "/movie.NoMercy.m3u8";
            await mutateContext.SaveChangesAsync();
        }

        Func<Task> act = async () => await service.DeleteItemAsync(itemId: item.Id, ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        storageFactory.Verify(
            expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()),
            times: Times.Never
        );

        service.Latest.Items.Should().HaveCount(expected: 1);
        service.Latest.Items[index: 0].Id.Should().Be(expected: item.Id);
    }

    [Fact]
    public async Task DeleteItemAsync_OriginalNoLongerOnDisk_AbortsAndDeletesNothing()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Vanished Original Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    driverId: driverId,
                    hostFolder: HostFolder,
                    filename: "/movie.mkv",
                    movieId: 6,
                    movieTitle: "Vanished Original Movie"
                )
        );
        using SqliteConnection _ = connection;

        // At scan time the original was still on disk alongside the HLS ladder — a
        // legitimate ReclaimableHls snapshot item.
        List<FolderEntry> scanEntries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 200_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        // Between scan and delete the original was removed from disk (e.g. a concurrent
        // delete or a not-yet-rescanned VideoFile row pointing at a gone file). Only the
        // HLS master + ladder remain — this folder is no longer ReclaimableHls, it is the
        // last playable copy's only remnant and must not be touched.
        List<StorageEntry> freshEntries =
        [
            new(Path: $"{HostFolder}/movie.NoMercy.m3u8", IsDirectory: false, SizeBytes: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(
                Path: $"{HostFolder}/video_1920x1080_SDR",
                IsDirectory: true,
                SizeBytes: 1_500_000_000,
                LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)
            ),
            new(Path: $"{HostFolder}/audio_eng", IsDirectory: true, SizeBytes: 200_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        Mock<IStorage> storage = new(behavior: MockBehavior.Strict);
        storage.Setup(expression: s => s.List(HostFolder, null, false)).Returns(value: freshEntries);
        storage
            .Setup(expression: s => s.GetName(It.IsAny<string>()))
            .Returns(
                valueFunction: (string path) =>
                {
                    int idx = path.LastIndexOf(value: '/');
                    return idx < 0 ? path : path[(idx + 1)..];
                }
            );

        Mock<IStorageFactory> storageFactory = new();
        storageFactory.Setup(expression: f => f.For(folderId, driverId, string.Empty)).Returns(value: storage.Object);

        ReclaimScanService service = new(
            contextFactory: factory,
            storageFactory: storageFactory.Object,
            configurationStore: new StubConfigurationStore(),
            logger: NullLogger<ReclaimScanService>.Instance,
            listFolderEntriesOverride: (_, _, _) => scanEntries
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(expected: 1);
        ReclaimableItem item = service.Latest.Items[index: 0];

        Func<Task> act = async () => await service.DeleteItemAsync(itemId: item.Id, ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        storage.Verify(expression: s => s.Delete(It.IsAny<string>()), times: Times.Never);
        storage.Verify(expression: s => s.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), times: Times.Never);

        service.Latest.Items.Should().HaveCount(expected: 1);
        service.Latest.Items[index: 0].Id.Should().Be(expected: item.Id);
        service.Latest.TotalReclaimableBytes.Should().Be(expected: item.ReclaimableBytes);
    }

    [Fact]
    public async Task DeleteItemAsync_UnknownId_ThrowsKeyNotFound()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Known Movie (2020)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    driverId: driverId,
                    hostFolder: HostFolder,
                    filename: "/movie.mkv",
                    movieId: 3,
                    movieTitle: "Known Movie"
                )
        );
        using SqliteConnection _ = connection;

        Mock<IStorageFactory> storageFactory = new(behavior: MockBehavior.Strict);

        ReclaimScanService service = new(
            contextFactory: factory,
            storageFactory: storageFactory.Object,
            configurationStore: new StubConfigurationStore(),
            logger: NullLogger<ReclaimScanService>.Instance,
            listFolderEntriesOverride: (_, _, _) =>
                [
                    new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
                    new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
                    new(
                        Name: "video_1920x1080_SDR",
                        IsDirectory: true,
                        Size: 1_500_000_000,
                        LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)
                    ),
                ]
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        Func<Task> act = async () =>
            await service.DeleteItemAsync(itemId: "unknown-item-id", ct: CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SweepPartialsAsync_DeletesStalePartials_SkipsFolderThatRegainedMaster()
    {
        Ulid staleFolderId = Ulid.NewUlid();
        Ulid staleDriverId = Ulid.NewUlid();
        const string StaleHostFolder = "movies/Stale Partial Movie (2021)";

        Ulid revivedFolderId = Ulid.NewUlid();
        Ulid revivedDriverId = Ulid.NewUlid();
        const string RevivedHostFolder = "movies/Revived Partial Movie (2022)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
            {
                SeedMovieVideoFile(
                    context: context,
                    folderId: staleFolderId,
                    driverId: staleDriverId,
                    hostFolder: StaleHostFolder,
                    filename: "/movie.mkv",
                    movieId: 4,
                    movieTitle: "Stale Partial Movie"
                );
                SeedMovieVideoFile(
                    context: context,
                    folderId: revivedFolderId,
                    driverId: revivedDriverId,
                    hostFolder: RevivedHostFolder,
                    filename: "/movie.mkv",
                    movieId: 5,
                    movieTitle: "Revived Partial Movie"
                );
            }
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> staleScanEntries =
        [
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 900_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)),
            new(Name: "audio_eng", IsDirectory: true, Size: 100_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)),
        ];

        List<FolderEntry> revivedScanEntries =
        [
            new(Name: "video_1280x720_SDR", IsDirectory: true, Size: 400_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)),
        ];

        Dictionary<string, IReadOnlyList<FolderEntry>> scanEntriesByFolder = new()
        {
            [key: StaleHostFolder] = staleScanEntries,
            [key: RevivedHostFolder] = revivedScanEntries,
        };

        List<StorageEntry> staleFreshEntries =
        [
            new(
                Path: $"{StaleHostFolder}/video_1920x1080_SDR",
                IsDirectory: true,
                SizeBytes: 900_000_000,
                LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)
            ),
            new(
                Path: $"{StaleHostFolder}/audio_eng",
                IsDirectory: true,
                SizeBytes: 100_000_000,
                LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)
            ),
        ];

        // The folder regained a master playlist since the scan — no longer masterless.
        List<StorageEntry> revivedFreshEntries =
        [
            new(
                Path: $"{RevivedHostFolder}/video_1280x720_SDR",
                IsDirectory: true,
                SizeBytes: 400_000_000,
                LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)
            ),
            new(
                Path: $"{RevivedHostFolder}/movie.NoMercy.m3u8",
                IsDirectory: false,
                SizeBytes: 600,
                LastModified: DateTimeOffset.UtcNow.AddMinutes(minutes: -5)
            ),
        ];

        Mock<IStorage> staleStorage = BuildStorageMock(hostFolder: StaleHostFolder, entries: staleFreshEntries);
        Mock<IStorage> revivedStorage = BuildStorageMock(hostFolder: RevivedHostFolder, entries: revivedFreshEntries);

        Mock<IStorageFactory> storageFactory = new();
        storageFactory
            .Setup(expression: f => f.For(staleFolderId, staleDriverId, string.Empty))
            .Returns(value: staleStorage.Object);
        storageFactory
            .Setup(expression: f => f.For(revivedFolderId, revivedDriverId, string.Empty))
            .Returns(value: revivedStorage.Object);

        ReclaimScanService service = new(
            contextFactory: factory,
            storageFactory: storageFactory.Object,
            configurationStore: new StubConfigurationStore(),
            logger: NullLogger<ReclaimScanService>.Instance,
            listFolderEntriesOverride: (_, _, hostFolder) => scanEntriesByFolder[key: hostFolder]
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.PartialJunk.Should().HaveCount(expected: 2);

        (int count, long bytes) result = await service.SweepPartialsAsync(ct: CancellationToken.None);

        result.count.Should().Be(expected: 1);
        result.bytes.Should().Be(expected: 900_000_000 + 100_000_000);

        staleStorage.Verify(
            expression: s => s.DeleteDirectory($"{StaleHostFolder}/video_1920x1080_SDR", true),
            times: Times.Once
        );
        staleStorage.Verify(
            expression: s => s.DeleteDirectory($"{StaleHostFolder}/audio_eng", true),
            times: Times.Once
        );

        revivedStorage.Verify(
            expression: s => s.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()),
            times: Times.Never
        );
        revivedStorage.Verify(expression: s => s.Delete(It.IsAny<string>()), times: Times.Never);

        service.Latest.PartialJunk.Should().HaveCount(expected: 1);
        service.Latest.PartialJunk[index: 0].Folder.Should().Be(expected: RevivedHostFolder);
        service.Latest.TotalPartialJunkBytes.Should().Be(expected: 400_000_000);
    }

    private sealed class StubConfigurationStore(string? partialStaleHoursValue = null)
        : IConfigurationStore
    {
        public string? GetValue(string key) =>
            key == "reclaim.partial_stale_hours" ? partialStaleHoursValue : null;

        public void SetValue(string key, string value) { }

        public Task SetValueAsync(string key, string value, Guid? modifiedBy = null) =>
            Task.CompletedTask;

        public bool HasKey(string key) => false;
    }
}
