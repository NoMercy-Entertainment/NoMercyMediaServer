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

[Trait("Category", "Unit")]
public class ReclaimScanServiceTests
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

    private static Folder SeedFolder(MediaContext context, Ulid folderId, string path) =>
        Seed(
            context,
            new Folder
            {
                Id = folderId,
                Path = path,
                DriverId = Ulid.NewUlid(),
            }
        );

    private static T Seed<T>(MediaContext context, T entity)
        where T : class
    {
        context.Add(entity);
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
        SeedFolder(context, folderId, hostFolder);

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

        SeedFolder(context, folderId, hostFolder);

        Seed(
            context,
            new Tv
            {
                Id = tvId,
                Title = showTitle,
                TitleSort = showTitle.ToLowerInvariant(),
                LibraryId = Ulid.NewUlid(),
            }
        );

        Seed(
            context,
            new Season
            {
                Id = seasonId,
                Title = $"Season {seasonNumber}",
                SeasonNumber = seasonNumber,
                TvId = tvId,
            }
        );

        Seed(
            context,
            new Episode
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
            context,
            new VideoFile
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
            contextFactory,
            new Mock<IStorageFactory>(MockBehavior.Strict).Object,
            configurationStore ?? new StubConfigurationStore(),
            NullLogger<ReclaimScanService>.Instance,
            listFolderEntries
        );

    private static async Task WaitUntilNotScanningAsync(IReclaimScanService service)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (service.State == ReclaimScanState.Scanning && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Fact]
    public async Task StartScanAsync_HlsServedFolder_IsProtected_NotInItems()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Protected Movie (2020)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "movie.NoMercy.m3u8",
                    1,
                    "Protected Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        ReclaimScanService service = BuildService(factory, (_, _, _) => entries);

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.State.Should().Be(ReclaimScanState.Completed);
        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().BeEmpty();
        service.Latest.PartialJunk.Should().BeEmpty();
        service.Latest.TotalReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public async Task StartScanAsync_OriginalServedFolder_CompleteHlsOnDisk_AppearsInItems()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Reclaimable Movie (2019)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "movie.mkv",
                    2,
                    "Reclaimable Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, DateTimeOffset.UtcNow.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
            new("audio_eng", true, 200_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        ReclaimScanService service = BuildService(factory, (_, _, _) => entries);

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(1);
        ReclaimableItem item = service.Latest.Items[0];
        item.Title.Should().Be("Reclaimable Movie");
        item.MediaType.Should().Be("movie");
        item.Folder.Should().Be(HostFolder);
        item.ServedCopy.Should().Be("movie.mkv");
        item.Kind.Should().Be(ReclaimKind.ReclaimableHls);
        item.ReclaimableBytes.Should().Be(500 + 1_500_000_000 + 200_000_000);
        item.TargetPaths.Should()
            .BeEquivalentTo([
                $"{HostFolder}/video_1920x1080_SDR",
                $"{HostFolder}/audio_eng",
                $"{HostFolder}/movie.NoMercy.m3u8",
            ]);
        service.Latest.TotalReclaimableBytes.Should().Be(item.ReclaimableBytes);
        service.Latest.PartialJunk.Should().BeEmpty();
    }

    [Fact]
    public async Task StartScanAsync_MasterlessStaleLadder_AppearsInPartialJunk()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Partial Movie (2021)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "movie.mkv",
                    3,
                    "Partial Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new("video_1920x1080_SDR", true, 900_000_000, DateTimeOffset.UtcNow.AddDays(-10)),
            new("audio_eng", true, 100_000_000, DateTimeOffset.UtcNow.AddDays(-10)),
        ];

        ReclaimScanService service = BuildService(factory, (_, _, _) => entries);

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().BeEmpty();
        service.Latest.PartialJunk.Should().HaveCount(1);
        PartialJunkItem junk = service.Latest.PartialJunk[0];
        junk.Folder.Should().Be(HostFolder);
        junk.Bytes.Should().Be(900_000_000 + 100_000_000);
        junk.TargetPaths.Should()
            .BeEquivalentTo([$"{HostFolder}/video_1920x1080_SDR", $"{HostFolder}/audio_eng"]);
        service.Latest.TotalPartialJunkBytes.Should().Be(junk.Bytes);
    }

    [Fact]
    public async Task StartScanAsync_State_TransitionsIdleToCompleted_AndSetsLastScannedAt()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Untouched Movie (2022)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "movie.mkv",
                    4,
                    "Untouched Movie"
                )
        );
        using SqliteConnection _ = connection;

        ReclaimScanService service = BuildService(
            factory,
            (_, _, _) => [new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow)]
        );

        service.State.Should().Be(ReclaimScanState.Idle);
        service.LastScannedAt.Should().BeNull();

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.State.Should().Be(ReclaimScanState.Completed);
        service.LastScannedAt.Should().NotBeNull();
        service.Latest.Should().NotBeNull();
    }

    [Fact]
    public async Task StartScanAsync_SecondCallWhileScanning_DoesNotStartConcurrentScan()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Gated Movie (2023)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "movie.mkv",
                    5,
                    "Gated Movie"
                )
        );
        using SqliteConnection _ = connection;

        using SemaphoreSlim gate = new(0, 1);
        int listCallCount = 0;

        ReclaimScanService service = BuildService(
            factory,
            (_, _, _) =>
            {
                Interlocked.Increment(ref listCallCount);
                gate.Wait(TimeSpan.FromSeconds(5));
                return [new("movie.mkv", false, 4_000_000_000, DateTimeOffset.UtcNow)];
            }
        );

        await service.StartScanAsync(CancellationToken.None);
        service.State.Should().Be(ReclaimScanState.Scanning);

        await service.StartScanAsync(CancellationToken.None);
        service.State.Should().Be(ReclaimScanState.Scanning);

        gate.Release();
        await WaitUntilNotScanningAsync(service);

        service.State.Should().Be(ReclaimScanState.Completed);
        listCallCount.Should().Be(1);
    }

    [Fact]
    public async Task StartScanAsync_PartialStaleHoursOverride_IsHonored()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "movies/Recent Partial Movie (2024)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedMovieVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "movie.mkv",
                    6,
                    "Recent Partial Movie"
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new("video_1920x1080_SDR", true, 900_000_000, DateTimeOffset.UtcNow.AddHours(-2)),
        ];

        ReclaimScanService defaultService = BuildService(factory, (_, _, _) => entries);
        await defaultService.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(defaultService);
        defaultService.Latest!.PartialJunk.Should().BeEmpty();

        ReclaimScanService overriddenService = BuildService(
            factory,
            (_, _, _) => entries,
            new StubConfigurationStore("1")
        );
        await overriddenService.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(overriddenService);

        overriddenService.Latest.Should().NotBeNull();
        overriddenService.Latest!.PartialJunk.Should().HaveCount(1);
        overriddenService.Latest.PartialJunk[0].Folder.Should().Be(HostFolder);
    }

    [Fact]
    public async Task StartScanAsync_EpisodeServedFolder_ResolvesShowTitleAndTvMediaType()
    {
        Ulid folderId = Ulid.NewUlid();
        const string HostFolder = "tv/Some Show/Season 01";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
                SeedEpisodeVideoFile(
                    context,
                    folderId,
                    HostFolder,
                    "episode.mkv",
                    42,
                    "Some Show",
                    1,
                    3
                )
        );
        using SqliteConnection _ = connection;

        List<FolderEntry> entries =
        [
            new("episode.mkv", false, 2_000_000_000, DateTimeOffset.UtcNow.AddDays(-30)),
            new("episode.NoMercy.m3u8", false, 400, DateTimeOffset.UtcNow.AddDays(-1)),
            new("video_1280x720_SDR", true, 800_000_000, DateTimeOffset.UtcNow.AddDays(-1)),
        ];

        ReclaimScanService service = BuildService(factory, (_, _, _) => entries);

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(1);
        ReclaimableItem item = service.Latest.Items[0];
        item.Title.Should().Be("Some Show S01E03");
        item.MediaType.Should().Be("tv");
    }

    [Fact]
    public async Task StartScanAsync_NoOverride_UsesRealStorageLister_MapsFolderDriverAndHostFolder()
    {
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        const string HostFolder = "movies/Real Storage Movie (2020)";

        IDbContextFactory<MediaContext> factory = ContextFactory(
            out SqliteConnection connection,
            context =>
            {
                Seed(
                    context,
                    new Folder
                    {
                        Id = folderId,
                        Path = HostFolder,
                        DriverId = driverId,
                    }
                );
                Seed(
                    context,
                    new Movie
                    {
                        Id = 100,
                        Title = "Real Storage Movie",
                        TitleSort = "real storage movie",
                        LibraryId = Ulid.NewUlid(),
                    }
                );
                Seed(
                    context,
                    new VideoFile
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

        Mock<IStorage> storage = new();
        storage.Setup(s => s.List(HostFolder, null, false)).Returns(storageEntries);
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
            NullLogger<ReclaimScanService>.Instance
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        storageFactory.Verify(f => f.For(folderId, driverId, string.Empty), Times.Once);
        storage.Verify(s => s.List(HostFolder, null, false), Times.Once);

        service.State.Should().Be(ReclaimScanState.Completed);
        service.Latest.Should().NotBeNull();
        service.Latest!.Items.Should().HaveCount(1);
        ReclaimableItem item = service.Latest.Items[0];
        item.Title.Should().Be("Real Storage Movie");
        item.MediaType.Should().Be("movie");
        item.Folder.Should().Be(HostFolder);
        item.ServedCopy.Should().Be("movie.mkv");
        item.Kind.Should().Be(ReclaimKind.ReclaimableHls);
        item.ReclaimableBytes.Should().Be(500 + 1_500_000_000 + 200_000_000);
        item.TargetPaths.Should()
            .BeEquivalentTo([
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
            out SqliteConnection connection,
            context =>
            {
                Seed(
                    context,
                    new Movie
                    {
                        Id = 7,
                        Title = "Bad Share Movie",
                        TitleSort = "bad share movie",
                        LibraryId = Ulid.NewUlid(),
                    }
                );
                Seed(
                    context,
                    new VideoFile
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
            factory,
            (_, _, _) =>
            {
                listWasCalled = true;
                return [];
            }
        );

        await service.StartScanAsync(CancellationToken.None);
        await WaitUntilNotScanningAsync(service);

        service.State.Should().Be(ReclaimScanState.Completed);
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
