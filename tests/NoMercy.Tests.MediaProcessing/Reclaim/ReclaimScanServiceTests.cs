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
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Reclaim;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.MediaProcessing.Reclaim;

[Trait(name: "Category", value: "Unit")]
public class ReclaimScanServiceTests
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

    private static Folder SeedFolder(MediaContext context, Ulid folderId, string path) =>
        Seed(
            context: context,
            entity: new Folder
            {
                Id = folderId,
                Path = path,
                DriverId = Ulid.NewUlid(),
            }
        );

    private static T Seed<T>(MediaContext context, T entity)
        where T : class
    {
        context.Add(entity: entity);
        return entity;
    }

    private static VideoFile SeedMovieVideoFile(
        MediaContext context,
        Ulid folderId,
        string hostFolder,
        string filename,
        int movieId,
        string movieTitle
    )
    {
        SeedFolder(context: context, folderId: folderId, path: hostFolder);

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

    private static VideoFile SeedEpisodeVideoFile(
        MediaContext context,
        Ulid folderId,
        string hostFolder,
        string filename,
        int episodeId,
        string showTitle,
        int seasonNumber,
        int episodeNumber
    )
    {
        int tvId = episodeId + 10_000;
        int seasonId = episodeId + 20_000;

        SeedFolder(context: context, folderId: folderId, path: hostFolder);

        Seed(
            context: context,
            entity: new Tv
            {
                Id = tvId,
                Title = showTitle,
                TitleSort = showTitle.ToLowerInvariant(),
                LibraryId = Ulid.NewUlid(),
            }
        );

        Seed(
            context: context,
            entity: new Season
            {
                Id = seasonId,
                Title = $"Season {seasonNumber}",
                SeasonNumber = seasonNumber,
                TvId = tvId,
            }
        );

        Seed(
            context: context,
            entity: new Episode
            {
                Id = episodeId,
                Title = $"Episode {episodeNumber}",
                EpisodeNumber = episodeNumber,
                SeasonNumber = seasonNumber,
                TvId = tvId,
                SeasonId = seasonId,
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
                EpisodeId = episodeId,
            }
        );
    }

    private static ReclaimScanService BuildService(
        IDbContextFactory<MediaContext> contextFactory,
        Func<Ulid, Ulid, string, IReadOnlyList<FolderEntry>> listFolderEntries,
        IConfigurationStore? configurationStore = null
    ) =>
        new(
            contextFactory: contextFactory,
            storageFactory: new Mock<IStorageFactory>(behavior: MockBehavior.Strict).Object,
            configurationStore: configurationStore ?? new StubConfigurationStore(),
            logger: NullLogger<ReclaimScanService>.Instance,
            listFolderEntriesOverride: listFolderEntries
        );

    private static async Task WaitUntilNotScanningAsync(IReclaimScanService service)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(seconds: 5);
        while (service.State == ReclaimScanState.Scanning && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 10);
    }

    [Fact]
    public async Task StartScanAsync_HlsServedFolder_IsProtected_NotInItems()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Protected Movie (2020)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "movie.NoMercy.m3u8",
                    movieId: 1,
                    movieTitle: "Protected Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        ReclaimScanService service = BuildService(contextFactory: factory, listFolderEntries: (_, _, _) => entries);

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.State.Should().Be(expected: ReclaimScanState.Completed);
        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().BeEmpty();
        service.Latest.PartialJunk.Should().BeEmpty();
        service.Latest.TotalReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public async Task StartScanAsync_OriginalServedFolder_CompleteHlsOnDisk_AppearsInItems()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Reclaimable Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "movie.mkv",
                    movieId: 2,
                    movieTitle: "Reclaimable Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 200_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        ReclaimScanService service = BuildService(contextFactory: factory, listFolderEntries: (_, _, _) => entries);

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(expected: 1);
        ReclaimableItem item = service.Latest.Items[index: 0];
        item.Title.Should().Be(expected: "Reclaimable Movie");
        item.MediaType.Should().Be(expected: "movie");
        item.Folder.Should().Be(expected: HostFolder);
        item.ServedCopy.Should().Be(expected: "movie.mkv");
        item.Kind.Should().Be(expected: ReclaimKind.ReclaimableHls);
        item.ReclaimableBytes.Should().Be(expected: 500 + 1_500_000_000 + 200_000_000);
        item.TargetPaths.Should()
            .BeEquivalentTo(expectation:
            [
                $"{HostFolder}/video_1920x1080_SDR",
                $"{HostFolder}/audio_eng",
                $"{HostFolder}/movie.NoMercy.m3u8",
            ]);
        service.Latest.TotalReclaimableBytes.Should().Be(expected: item.ReclaimableBytes);
        service.Latest.PartialJunk.Should().BeEmpty();
    }

    [Fact]
    public async Task StartScanAsync_MasterlessStaleLadder_AppearsInPartialJunk()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Partial Movie (2021)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "movie.mkv",
                    movieId: 3,
                    movieTitle: "Partial Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 900_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)),
            new(Name: "audio_eng", IsDirectory: true, Size: 100_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -10)),
        ];

        ReclaimScanService service = BuildService(contextFactory: factory, listFolderEntries: (_, _, _) => entries);

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().BeEmpty();
        service.Latest.PartialJunk.Should().HaveCount(expected: 1);
        PartialJunkItem junk = service.Latest.PartialJunk[index: 0];
        junk.Folder.Should().Be(expected: HostFolder);
        junk.Bytes.Should().Be(expected: 900_000_000 + 100_000_000);
        junk.TargetPaths.Should()
            .BeEquivalentTo(expectation: [$"{HostFolder}/video_1920x1080_SDR", $"{HostFolder}/audio_eng"]);
        service.Latest.TotalPartialJunkBytes.Should().Be(expected: junk.Bytes);
    }

    [Fact]
    public async Task StartScanAsync_State_TransitionsIdleToCompleted_AndSetsLastScannedAt()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Untouched Movie (2022)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "movie.mkv",
                    movieId: 4,
                    movieTitle: "Untouched Movie"
                )
        );
        using SqliteConnection _ = connection;

        ReclaimScanService service = BuildService(
            contextFactory: factory,
            listFolderEntries: (_, _, _) => [new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow)]
        );

        service.State.Should().Be(expected: ReclaimScanState.Idle);
        service.LastScannedAt.Should().BeNull();

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.State.Should().Be(expected: ReclaimScanState.Completed);
        service.LastScannedAt.Should().NotBeNull();
        service.Latest.Should().NotBeNull();
    }

    [Fact]
    public async Task StartScanAsync_SecondCallWhileScanning_DoesNotStartConcurrentScan()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Gated Movie (2023)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "movie.mkv",
                    movieId: 5,
                    movieTitle: "Gated Movie"
                )
        );
        using SqliteConnection _ = connection;

        using SemaphoreSlim gate = new(initialCount: 0, maxCount: 1);
        int listCallCount = 0;

        ReclaimScanService service = BuildService(
            contextFactory: factory,
            listFolderEntries: (_, _, _) =>
            {
                Interlocked.Increment(location: ref listCallCount);
                gate.Wait(timeout: TimeSpan.FromSeconds(seconds: 5));
                return [new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: DateTimeOffset.UtcNow)];
            }
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        service.State.Should().Be(expected: ReclaimScanState.Scanning);

        await service.StartScanAsync(ct: CancellationToken.None);
        service.State.Should().Be(expected: ReclaimScanState.Scanning);

        gate.Release();
        await WaitUntilNotScanningAsync(service: service);

        service.State.Should().Be(expected: ReclaimScanState.Completed);
        listCallCount.Should().Be(expected: 1);
    }

    [Fact]
    public async Task StartScanAsync_PartialStaleHoursOverride_IsHonored()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Recent Partial Movie (2024)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedMovieVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "movie.mkv",
                    movieId: 6,
                    movieTitle: "Recent Partial Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 900_000_000, LastModified: DateTimeOffset.UtcNow.AddHours(hours: -2)),
        ];

        ReclaimScanService defaultService = BuildService(contextFactory: factory, listFolderEntries: (_, _, _) => entries);
        await defaultService.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: defaultService);
        defaultService.Latest!.PartialJunk.Should().BeEmpty();

        ReclaimScanService overriddenService = BuildService(
            contextFactory: factory,
            listFolderEntries: (_, _, _) => entries,
            configurationStore: new StubConfigurationStore(partialStaleHoursValue: "1")
        );
        await overriddenService.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: overriddenService);

        overriddenService.Latest.Should().NotBeNull();
        overriddenService.Latest!.PartialJunk.Should().HaveCount(expected: 1);
        overriddenService.Latest.PartialJunk[index: 0].Folder.Should().Be(expected: HostFolder);
    }

    [Fact]
    public async Task StartScanAsync_EpisodeServedFolder_ResolvesShowTitleAndTvMediaType()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "tv/Some Show/Season 01";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
                SeedEpisodeVideoFile(
                    context: context,
                    folderId: folderId,
                    hostFolder: HostFolder,
                    filename: "episode.mkv",
                    episodeId: 42,
                    showTitle: "Some Show",
                    seasonNumber: 1,
                    episodeNumber: 3
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new(Name: "episode.mkv", IsDirectory: false, Size: 2_000_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -30)),
            new(Name: "episode.NoMercy.m3u8", IsDirectory: false, Size: 400, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
            new(Name: "video_1280x720_SDR", IsDirectory: true, Size: 800_000_000, LastModified: DateTimeOffset.UtcNow.AddDays(days: -1)),
        ];

        ReclaimScanService service = BuildService(contextFactory: factory, listFolderEntries: (_, _, _) => entries);

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(expected: 1);
        ReclaimableItem item = service.Latest.Items[index: 0];
        item.Title.Should().Be(expected: "Some Show S01E03");
        item.MediaType.Should().Be(expected: "tv");
    }

    [Fact]
    public async Task StartScanAsync_NoOverride_UsesRealStorageLister_MapsFolderDriverAndHostFolder()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Real Storage Movie (2020)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
            {
                Seed(
                    context: context,
                    entity: new Folder
                    {
                        Id = folderId,
                        Path = HostFolder,
                        DriverId = driverId,
                    }
                );
                Seed(
                    context: context,
                    entity: new Movie
                    {
                        Id = 100,
                        Title = "Real Storage Movie",
                        TitleSort = "real storage movie",
                        LibraryId = Ulid.NewUlid(),
                    }
                );
                Seed(
                    context: context,
                    entity: new VideoFile
                    {
                        Id = Ulid.NewUlid(),
                        Filename = "movie.mkv",
                        Folder = HostFolder,
                        HostFolder = HostFolder,
                        Share = folderId.ToString(),
                        MovieId = 100,
                    }
                );
            }
        );
        using SqliteConnection _ = connection;

        List<StorageEntry> storageEntries =
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

        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.List(HostFolder, null, false)).Returns(value: storageEntries);
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
            logger: NullLogger<ReclaimScanService>.Instance
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        storageFactory.Verify(expression: f => f.For(folderId, driverId, string.Empty), times: Times.Once);
        storage.Verify(expression: s => s.List(HostFolder, null, false), times: Times.Once);

        service.State.Should().Be(expected: ReclaimScanState.Completed);
        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(expected: 1);
        ReclaimableItem item = service.Latest.Items[index: 0];
        item.Title.Should().Be(expected: "Real Storage Movie");
        item.MediaType.Should().Be(expected: "movie");
        item.Folder.Should().Be(expected: HostFolder);
        item.ServedCopy.Should().Be(expected: "movie.mkv");
        item.Kind.Should().Be(expected: ReclaimKind.ReclaimableHls);
        item.ReclaimableBytes.Should().Be(expected: 500 + 1_500_000_000 + 200_000_000);
        item.TargetPaths.Should()
            .BeEquivalentTo(expectation:
            [
                $"{HostFolder}/video_1920x1080_SDR",
                $"{HostFolder}/audio_eng",
                $"{HostFolder}/movie.NoMercy.m3u8",
            ]);
    }

    [Fact]
    public async Task StartScanAsync_UnparseableShare_SkipsFolderWithoutCrashingScan()
    {
        const string HostFolder = "movies/Bad Share Movie (2018)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            connection: out SqliteConnection connection,
            seed: context =>
            {
                Seed(
                    context: context,
                    entity: new Movie
                    {
                        Id = 7,
                        Title = "Bad Share Movie",
                        TitleSort = "bad share movie",
                        LibraryId = Ulid.NewUlid(),
                    }
                );
                Seed(
                    context: context,
                    entity: new VideoFile
                    {
                        Id = Ulid.NewUlid(),
                        Filename = "movie.mkv",
                        Folder = HostFolder,
                        HostFolder = HostFolder,
                        Share = "not-a-folder-id",
                        MovieId = 7,
                    }
                );
            }
        );
        using SqliteConnection _ = connection;

        bool listWasCalled = false;
        ReclaimScanService service = BuildService(
            contextFactory: factory,
            listFolderEntries: (_, _, _) =>
            {
                listWasCalled = true;
                return [];
            }
        );

        await service.StartScanAsync(ct: CancellationToken.None);
        await WaitUntilNotScanningAsync(service: service);

        service.State.Should().Be(expected: ReclaimScanState.Completed);
        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().BeEmpty();
        service.Latest.PartialJunk.Should().BeEmpty();
        listWasCalled.Should().BeFalse();
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
