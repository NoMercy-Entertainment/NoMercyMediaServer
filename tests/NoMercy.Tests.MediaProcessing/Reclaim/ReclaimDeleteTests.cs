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

[Trait("Category", "Unit")]
public class ReclaimDeleteTests
{
    private static IDbContextFactory<MediaContext> ContextFactory(
        out SqliteConnection connection,
        Action<MediaContext> seed
    )
    {
        SqliteConnection conn = new("DataSource=:memory:");
        conn.Open();
        connection = conn;

        using (SqliteCommand pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(conn)
            .Options;

        using (MediaContext seedContext = new(options))
        {
            seedContext.Database.EnsureCreated();
            seed(seedContext);
            seedContext.SaveChanges();
        }

        Mock<IDbContextFactory<MediaContext>> factory = new();
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new(options));
        factory.Setup(f => f.CreateDbContext()).Returns(() => new(options));
        return factory.Object;
    }

    private static T Seed<T>(MediaContext context, T entity)
        where T : class
    {
        context.Add(entity);
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
            context,
            new Folder
            {
                Id = folderId,
                Path = hostFolder,
                DriverId = driverId,
            }
        );

        Seed(
            context,
            new Movie
            {
                Id = movieId,
                Title = movieTitle,
                TitleSort = movieTitle.ToLowerInvariant(),
                LibraryId = Ulid.NewUlid(),
            }
        );

        return Seed(
            context,
            new VideoFile
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
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (service.State == ReclaimScanState.Scanning && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    private static Mock<IStorage> BuildStorageMock(string hostFolder, List<StorageEntry> entries)
    {
        Mock<IStorage> storage = new();
        storage.Setup(s => s.List(hostFolder, null, false)).Returns(entries);
        storage
            .Setup(s => s.GetName(It.IsAny<string>()))
            .Returns(
                (string path) =>
                {
                    int idx = path.LastIndexOf('/');
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
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    driverId,
                    HostFolder,
                    "/movie.mkv",
                    1,
                    "Reclaimable Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> scanEntries =
        [
            new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
            new("audio_eng", true, 200_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        List<StorageEntry> freshEntries =
        [
            new(
                $"{HostFolder}/movie.mkv",
                false,
                4_000_000_000,
                DateTimeOffset.UtcNow.AddDays(-30)
            ),
            new($"{HostFolder}/movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new(
                $"{HostFolder}/video_1920x1080_SDR",
                true,
                1_500_000_000,
                DateTimeOffset.UtcNow.AddDays(-1)
            ),
            new($"{HostFolder}/audio_eng", true, 200_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        Mock<IStorage> storage = BuildStorageMock(HostFolder, freshEntries);
        Mock<IStorageFactory> storageFactory = new();
        storageFactory.Setup(f => f.For(folderId, driverId, string.Empty)).Returns(storage.Object);

        ReclaimScanService service = new(
            factory,
            storageFactory.Object,
            new StubConfigurationStore(),
            NullLogger<ReclaimScanService>.Instance,
            (_, _, _) => scanEntries
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(1);
        ReclaimableItem item = service.Latest.Items[0];

        long freedBytes = await service.DeleteItemAsync(item.Id, CancellationToken.None);

        freedBytes.Should().Be(500 + 1_500_000_000 + 200_000_000);

        storage.Verify(
            s => s.DeleteDirectory($"{HostFolder}/video_1920x1080_SDR", true),
            Times.Once
        );
        storage.Verify(s => s.DeleteDirectory($"{HostFolder}/audio_eng", true), Times.Once);
        storage.Verify(s => s.Delete($"{HostFolder}/movie.NoMercy.m3u8"), Times.Once);
        storage.Verify(s => s.Delete($"{HostFolder}/movie.mkv"), Times.Never);
        storage.Verify(
            s => s.DeleteDirectory($"{HostFolder}/movie.mkv", It.IsAny<bool>()),
            Times.Never
        );

        service.Latest.Items.Should().BeEmpty();
        service.Latest.TotalReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public async Task DeleteItemAsync_TargetNowMatchesServedCopy_RefusesAndDeletesNothing()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Race Condition Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    driverId,
                    HostFolder,
                    "/movie.mkv",
                    2,
                    "Race Condition Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> scanEntries =
        [
            new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
            new("audio_eng", true, 200_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        // Strict: any call to the storage factory during the guarded refusal fails the test —
        // the served-copy check must abort before storage is ever touched.
        Mock<IStorageFactory> storageFactory = new(MockBehavior.Strict);

        ReclaimScanService service = new(
            factory,
            storageFactory.Object,
            new StubConfigurationStore(),
            NullLogger<ReclaimScanService>.Instance,
            (_, _, _) => scanEntries
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(1);
        ReclaimableItem item = service.Latest.Items[0];
        item.TargetPaths.Should().Contain($"{HostFolder}/movie.NoMercy.m3u8");

        // Simulate the race: between scan and delete, the served copy pointer moved to the
        // exact file the stale snapshot marked as a reclaim target. Production stores
        // VideoFile.Filename with a leading slash — use that real format, not a bare leaf,
        // so this test cannot stay green against a guard that stopped normalizing it.
        await using (
            MediaContext mutateContext = await factory.CreateDbContextAsync(CancellationToken.None)
        )
        {
            VideoFile row = await mutateContext.VideoFiles.SingleAsync(v =>
                v.HostFolder == HostFolder
            );
            row.Filename = "/movie.NoMercy.m3u8";
            await mutateContext.SaveChangesAsync();
        }

        Func<Task> act = async () => await service.DeleteItemAsync(item.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        storageFactory.Verify(
            f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()),
            Times.Never
        );

        service.Latest.Items.Should().HaveCount(1);
        service.Latest.Items[0].Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task DeleteItemAsync_OriginalNoLongerOnDisk_AbortsAndDeletesNothing()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Vanished Original Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    driverId,
                    HostFolder,
                    "/movie.mkv",
                    6,
                    "Vanished Original Movie"
                )
        );
        using SqliteConnection _ = connection;

        // At scan time the original was still on disk alongside the HLS ladder — a
        // legitimate ReclaimableHls snapshot item.
        List<FolderEntry> scanEntries =
        [
            new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
            new("audio_eng", true, 200_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        // Between scan and delete the original was removed from disk (e.g. a concurrent
        // delete or a not-yet-rescanned VideoFile row pointing at a gone file). Only the
        // HLS master + ladder remain — this folder is no longer ReclaimableHls, it is the
        // last playable copy's only remnant and must not be touched.
        List<StorageEntry> freshEntries =
        [
            new($"{HostFolder}/movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new(
                $"{HostFolder}/video_1920x1080_SDR",
                true,
                1_500_000_000,
                DateTimeOffset.UtcNow.AddDays(-1)
            ),
            new($"{HostFolder}/audio_eng", true, 200_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        Mock<IStorage> storage = new(MockBehavior.Strict);
        storage.Setup(s => s.List(HostFolder, null, false)).Returns(freshEntries);
        storage
            .Setup(s => s.GetName(It.IsAny<string>()))
            .Returns(
                (string path) =>
                {
                    int idx = path.LastIndexOf('/');
                    return idx < 0 ? path : path[(idx + 1)..];
                }
            );

        Mock<IStorageFactory> storageFactory = new();
        storageFactory.Setup(f => f.For(folderId, driverId, string.Empty)).Returns(storage.Object);

        ReclaimScanService service = new(
            factory,
            storageFactory.Object,
            new StubConfigurationStore(),
            NullLogger<ReclaimScanService>.Instance,
            (_, _, _) => scanEntries
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(1);
        ReclaimableItem item = service.Latest.Items[0];

        Func<Task> act = async () => await service.DeleteItemAsync(item.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        storage.Verify(s => s.Delete(It.IsAny<string>()), Times.Never);
        storage.Verify(s => s.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);

        service.Latest.Items.Should().HaveCount(1);
        service.Latest.Items[0].Id.Should().Be(item.Id);
        service.Latest.TotalReclaimableBytes.Should().Be(item.ReclaimableBytes);
    }

    [Fact]
    public async Task DeleteItemAsync_UnknownId_ThrowsKeyNotFound()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Known Movie (2020)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    driverId,
                    HostFolder,
                    "/movie.mkv",
                    3,
                    "Known Movie"
                )
        );
        using SqliteConnection _ = connection;

        Mock<IStorageFactory> storageFactory = new(MockBehavior.Strict);

        ReclaimScanService service = new(
            factory,
            storageFactory.Object,
            new StubConfigurationStore(),
            NullLogger<ReclaimScanService>.Instance,
            (_, _, _) =>
                [
                    new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
                    new("movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
                    new(
                        "video_1920x1080_SDR",
                        true,
                        1_500_000_000,
                        DateTimeOffset.UtcNow.AddDays(-1)
                    ),
                ]
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        Func<Task> act = async () =>
            await service.DeleteItemAsync("unknown-item-id", CancellationToken.None);

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
            out SqliteConnection connection,
            context =>
            {
                SeedMovieVideoFile(
                    context,
                    staleFolderId,
                    staleDriverId,
                    StaleHostFolder,
                    "/movie.mkv",
                    4,
                    "Stale Partial Movie"
                );
                SeedMovieVideoFile(
                    context,
                    revivedFolderId,
                    revivedDriverId,
                    RevivedHostFolder,
                    "/movie.mkv",
                    5,
                    "Revived Partial Movie"
                );
            }
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> staleScanEntries =
        [
            new("video_1920x1080_SDR", true, 900_000_000, DateTimeOffset.UtcNow.AddDays(-10)),
            new("audio_eng", true, 100_000_000, DateTimeOffset.UtcNow.AddDays(-10)),
        ];

        List<FolderEntry> revivedScanEntries =
        [
            new("video_1280x720_SDR", true, 400_000_000, DateTimeOffset.UtcNow.AddDays(-10)),
        ];

        Dictionary<string, IReadOnlyList<FolderEntry>> scanEntriesByFolder = new()
        {
            [StaleHostFolder] = staleScanEntries,
            [RevivedHostFolder] = revivedScanEntries,
        };

        List<StorageEntry> staleFreshEntries =
        [
            new(
                $"{StaleHostFolder}/video_1920x1080_SDR",
                true,
                900_000_000,
                DateTimeOffset.UtcNow.AddDays(-10)
            ),
            new(
                $"{StaleHostFolder}/audio_eng",
                true,
                100_000_000,
                DateTimeOffset.UtcNow.AddDays(-10)
            ),
        ];

        // The folder regained a master playlist since the scan — no longer masterless.
        List<StorageEntry> revivedFreshEntries =
        [
            new(
                $"{RevivedHostFolder}/video_1280x720_SDR",
                true,
                400_000_000,
                DateTimeOffset.UtcNow.AddDays(-10)
            ),
            new(
                $"{RevivedHostFolder}/movie.NoMercy.m3u8",
                false,
                600,
                DateTimeOffset.UtcNow.AddMinutes(-5)
            ),
        ];

        Mock<IStorage> staleStorage = BuildStorageMock(StaleHostFolder, staleFreshEntries);
        Mock<IStorage> revivedStorage = BuildStorageMock(RevivedHostFolder, revivedFreshEntries);

        Mock<IStorageFactory> storageFactory = new();
        storageFactory
            .Setup(f => f.For(staleFolderId, staleDriverId, string.Empty))
            .Returns(staleStorage.Object);
        storageFactory
            .Setup(f => f.For(revivedFolderId, revivedDriverId, string.Empty))
            .Returns(revivedStorage.Object);

        ReclaimScanService service = new(
            factory,
            storageFactory.Object,
            new StubConfigurationStore(),
            NullLogger<ReclaimScanService>.Instance,
            (_, _, hostFolder) => scanEntriesByFolder[hostFolder]
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.PartialJunk.Should().HaveCount(2);

        (int count, long bytes) result = await service.SweepPartialsAsync(CancellationToken.None);

        result.count.Should().Be(1);
        result.bytes.Should().Be(900_000_000 + 100_000_000);

        staleStorage.Verify(
            s => s.DeleteDirectory($"{StaleHostFolder}/video_1920x1080_SDR", true),
            Times.Once
        );
        staleStorage.Verify(
            s => s.DeleteDirectory($"{StaleHostFolder}/audio_eng", true),
            Times.Once
        );

        revivedStorage.Verify(
            s => s.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never
        );
        revivedStorage.Verify(s => s.Delete(It.IsAny<string>()), Times.Never);

        service.Latest.PartialJunk.Should().HaveCount(1);
        service.Latest.PartialJunk[0].Folder.Should().Be(RevivedHostFolder);
        service.Latest.TotalPartialJunkBytes.Should().Be(400_000_000);
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
