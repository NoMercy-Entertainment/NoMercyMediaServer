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
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Events.Inbox;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.MediaProcessing.Inbox;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.NmSystem.Domain;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Factory;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Tests.MediaProcessing.EventHandlers;

internal sealed class FileWatcherTestQueueContext : IQueueContext
{
    private int _nextId = 1;

    public List<QueueJobModel> Jobs { get; } = [];

    public void AddJob(QueueJobModel job)
    {
        job.Id = _nextId++;
        Jobs.Add(item: job);
    }

    public void RemoveJob(QueueJobModel job) => Jobs.RemoveAll(match: j => j.Id == job.Id);

    public QueueJobModel? GetNextJob(
        string queueName,
        byte maxAttempts,
        long? currentJobId,
        DateTime now
    ) => Jobs.FirstOrDefault(predicate: j => string.IsNullOrEmpty(value: queueName) || j.Queue == queueName);

    public QueueJobModel? FindJob(int id) => Jobs.FirstOrDefault(predicate: j => j.Id == id);

    public bool JobExists(string payload) => Jobs.Any(predicate: j => j.Payload == payload);

    public void UpdateJob(QueueJobModel job)
    {
        int idx = Jobs.FindIndex(match: j => j.Id == job.Id);
        if (idx >= 0)
            Jobs[index: idx] = job;
    }

    public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt)
    {
        QueueJobModel? job = Jobs.FirstOrDefault(predicate: j => j.Id == jobId);
        if (job is null)
            return;
        job.Payload = newPayload;
        job.AvailableAt = availableAt;
        job.ReservedAt = null;
    }

    public void ResetAllReservedJobs()
    {
        foreach (QueueJobModel job in Jobs)
            job.ReservedAt = null;
    }

    public IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc) =>
        Jobs.Where(predicate: j => j.ReservedAt < cutoffUtc).ToList();

    public bool IsParentFailed(int parentJobId) => false;

    public void AddFailedJob(FailedJobModel failedJob) { }

    public void RemoveFailedJob(FailedJobModel failedJob) { }

    public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job) =>
        RemoveJob(job: job);

    public FailedJobModel? FindFailedJob(int id) => null;

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null) => [];

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs() => [];

    public CronJobModel? FindCronJobByName(string name) => null;

    public void AddCronJob(CronJobModel cronJob) { }

    public void UpdateCronJob(CronJobModel cronJob) { }

    public void RemoveCronJob(CronJobModel cronJob) { }

    public void SaveChanges() { }

    public void Dispose() { }
}

internal sealed class FileWatcherTmdbMockFactory : IHttpClientFactory
{
    private const int MovieId = 550;
    private const int ShowId = 1396;

    public HttpClient CreateClient(string name) =>
        new(handler: new Handler()) { BaseAddress = new(uriString: "https://api.themoviedb.org/3/") };

    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;

            string? body = true switch
            {
                _ when path.Contains(value: "/search/movie") => SearchMovieJson(),
                _ when path.Contains(value: "/search/tv") => SearchTvJson(),
                _ when path.Contains(value: $"/movie/{MovieId}") => MovieDetailJson(),
                _ when path.Contains(value: $"/tv/{ShowId}") => TvDetailJson(),
                _ => null,
            };

            HttpResponseMessage response = body is null
                ? new(statusCode: HttpStatusCode.NotFound)
                : new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                {
                    Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
                };

            return Task.FromResult(result: response);
        }

        private static string SearchMovieJson() =>
            $$"""
                {"page":1,"results":[{"id":{{MovieId}},"title":"Fight Club","original_title":"Fight Club","release_date":"1999-10-15","overview":"","popularity":10.0,"vote_average":8.4,"vote_count":10000,"backdrop_path":null,"poster_path":null,"original_language":"en","adult":false,"video":false}],"total_pages":1,"total_results":1}
                """;

        private static string SearchTvJson() =>
            $$"""
                {"page":1,"results":[{"id":{{ShowId}},"name":"Breaking Bad","original_name":"Breaking Bad","first_air_date":"2008-01-20","overview":"","popularity":10.0,"vote_average":9.5,"vote_count":10000,"backdrop_path":null,"poster_path":null,"original_language":"en","origin_country":["US"],"genre_ids":[],"type":""}],"total_pages":1,"total_results":1}
                """;

        private static string MovieDetailJson() =>
            $$"""{"id":{{MovieId}},"title":"Fight Club","release_date":"1999-10-15","overview":""}""";

        private static string TvDetailJson() =>
            $$"""{"id":{{ShowId}},"name":"Breaking Bad","first_air_date":"2008-01-20","overview":""}""";
    }
}

[Collection(name: "EventBusProvider")]
[Trait(name: "Category", value: "Integration")]
public class FileWatcherEventHandlerCascadeTests : IDisposable
{
    private static readonly int ExpectedMovieId = 550;
    private static readonly int ExpectedShowId = 1396;

    private readonly FileWatcherTestQueueContext _queueContext;
    private readonly QueueRunner _queueRunner;
    private readonly string _tempRoot;
    private readonly IStorageDriver _localDriver;
    private readonly StorageFactory _storageFactory;

    public FileWatcherEventHandlerCascadeTests()
    {
        HttpClientProvider.Initialize(factory: new FileWatcherTmdbMockFactory());

        _queueContext = new();
        QueueConfiguration config = new() { WorkerCounts = new() { [key: "import"] = 0 } };
        _queueRunner = new(queueContext: _queueContext, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: "nm-fweh-" + Path.GetRandomFileName());
        Directory.CreateDirectory(path: _tempRoot);

        _localDriver = new LocalStorageDriver();
        _storageFactory = new(driver: _localDriver, logger: NullLogger<StorageFactory>.Instance);
    }

    public void Dispose()
    {
        HttpClientProvider.Initialize(factory: new TmdbMockHttpClientFactory());
        _queueRunner.StopAll();

        try
        {
            Directory.Delete(path: _tempRoot, recursive: true);
        }
        catch { }
    }

    private FileWatcherEventHandler MakeHandler() =>
        new(
            logger: NullLogger<FileWatcherEventHandler>.Instance,
            eventBus: new InMemoryEventBus(),
            storageDriver: _localDriver,
            storageFactory: _storageFactory
        );

    private string CreateMovieFolder(string name = "Fight Club (1999)")
    {
        string folder = Path.Combine(path1: _tempRoot, path2: name);
        Directory.CreateDirectory(path: folder);
        File.WriteAllBytes(path: Path.Combine(path1: folder, path2: "Fight.Club.1999.mkv"), bytes: [0x00, 0x00]);
        return folder;
    }

    private string CreateTvFolder(string name = "Breaking Bad (2008)")
    {
        string folder = Path.Combine(path1: _tempRoot, path2: name);
        Directory.CreateDirectory(path: folder);
        Directory.CreateDirectory(path: Path.Combine(path1: folder, path2: "Season 01"));
        File.WriteAllBytes(
            path: Path.Combine(path1: folder, path2: "Season 01", path3: "Breaking.Bad.S01E01.mkv"),
            bytes: [0x00, 0x00]
        );
        return folder;
    }

    [Fact]
    public async Task MovieLibrary_FileCreated_DispatchesMovieImportJob_WithCorrectTmdbId()
    {
        string movieFolder = CreateMovieFolder();

        FileWatcherEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = movieFolder,
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.MovieMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _queueContext.Jobs.Should().ContainSingle(because: "one movie folder → one MovieImportJob");

        QueueJobModel queued = _queueContext.Jobs[index: 0];
        queued.Queue.Should().Be(expected: "import");

        JObject payload = JObject.Parse(json: queued.Payload);
        string? typeName = payload[propertyName: "$type"]?.Value<string>();
        typeName.Should().Contain(expected: "MovieImportJob");

        int dispatchedId = payload[propertyName: "id"]?.Value<int>() ?? 0;
        dispatchedId.Should().Be(expected: ExpectedMovieId, because: "TMDB search returned movie id 550 (Fight Club)");

        string? libraryId = payload[propertyName: "libraryId"]?.Value<string>();
        libraryId.Should().Be(expected: @event.LibraryId.ToString());
    }

    [Fact]
    public async Task TvLibrary_FileCreated_DispatchesShowImportJob_WithCorrectTmdbId()
    {
        string tvFolder = CreateTvFolder();

        FileWatcherEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = tvFolder,
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.TvMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _queueContext.Jobs.Should().ContainSingle(because: "one TV folder → one ShowImportJob");

        QueueJobModel queued = _queueContext.Jobs[index: 0];
        queued.Queue.Should().Be(expected: "import");

        JObject payload = JObject.Parse(json: queued.Payload);
        string? typeName = payload[propertyName: "$type"]?.Value<string>();
        typeName.Should().Contain(expected: "ShowImportJob");

        int dispatchedId = payload[propertyName: "id"]?.Value<int>() ?? 0;
        dispatchedId
            .Should()
            .Be(expected: ExpectedShowId, because: "TMDB search returned TV show id 1396 (Breaking Bad)");

        string? libraryId = payload[propertyName: "libraryId"]?.Value<string>();
        libraryId.Should().Be(expected: @event.LibraryId.ToString());
    }

    [Fact]
    public async Task AnimeLibrary_FileCreated_DispatchesShowImportJob()
    {
        string tvFolder = CreateTvFolder(name: "Breaking Bad Anime (2008)");

        FileWatcherEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = tvFolder,
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.AnimeMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _queueContext.Jobs.Should().ContainSingle(because: "anime library uses TV path → ShowImportJob");

        JObject payload = JObject.Parse(json: _queueContext.Jobs[index: 0].Payload);
        string? typeName = payload[propertyName: "$type"]?.Value<string>();
        typeName.Should().Contain(expected: "ShowImportJob");
    }

    [Fact]
    public async Task InboxLibrary_FileCreated_DoesNotDispatchAnyJob()
    {
        string folder = Path.Combine(path1: _tempRoot, path2: "inbox");
        Directory.CreateDirectory(path: folder);
        File.WriteAllBytes(path: Path.Combine(path1: folder, path2: "somefile.mkv"), bytes: [0x00]);

        FileWatcherEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = folder,
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.InboxMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _queueContext
            .Jobs.Should()
            .BeEmpty(
                because: "inbox type exits immediately — FileWatcherEventHandler must not dispatch import jobs for inbox folders"
            );
    }

    [Fact]
    public async Task MovieLibrary_TmdbNoMatch_NoJobDispatched()
    {
        string folder = Path.Combine(path1: _tempRoot, path2: "XYZ Completely Unknown Movie ZZZZZ");
        Directory.CreateDirectory(path: folder);
        File.WriteAllBytes(path: Path.Combine(path1: folder, path2: "movie.mkv"), bytes: [0x00]);

        HttpClientProvider.Initialize(factory: new NoResultsTmdbFactory());

        FileWatcherEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = folder,
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.MovieMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _queueContext
            .Jobs.Should()
            .BeEmpty(because: "when TMDB returns no results the handler must not dispatch");
    }

    private sealed class NoResultsTmdbFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler: new Handler()) { BaseAddress = new(uriString: "https://api.themoviedb.org/3/") };

        private sealed class Handler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            ) =>
                Task.FromResult(
                    result: new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            content: """{"page":1,"results":[],"total_pages":0,"total_results":0}""",
                            encoding: Encoding.UTF8,
                            mediaType: "application/json"
                        ),
                    }
                );
        }
    }
}

[Collection(name: "EventBusProvider")]
[Trait(name: "Category", value: "Integration")]
public class InboxClassifierCascadeViaEventBusTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MediaContext _sharedContext;

    public InboxClassifierCascadeViaEventBusTests()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new(connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;

        _sharedContext = new(options: opts);
        _sharedContext.Database.EnsureCreated();
        _sharedContext.Database.ExecuteSqlRaw(sql: "PRAGMA foreign_keys = OFF;");
    }

    public void Dispose()
    {
        _sharedContext.Dispose();
        _connection.Dispose();
    }

    private MediaContext CreateContext()
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;
        return new(options: opts);
    }

    private Ulid SeedInboxLibrary(string folderPath)
    {
        Ulid libraryId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        _sharedContext.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Inbox",
                Type = MediaTypes.InboxMediaType,
            }
        );
        _sharedContext.Folders.Add(
            entity: new()
            {
                Id = folderId,
                Path = folderPath,
                DriverId = driverId,
            }
        );
        _sharedContext.FolderLibrary.Add(entity: new() { LibraryId = libraryId, FolderId = folderId });
        _sharedContext.SaveChanges();

        return libraryId;
    }

    private (
        InMemoryEventBus Bus,
        InboxClassifierEventHandler Handler,
        Mock<IStorageFactory> StorageMock
    ) BuildChain(string folderPath = "/inbox", string childFile = "item.mkv")
    {
        InMemoryEventBus bus = new();

        Mock<IInboxMetadataProbe> probeMock = new();
        probeMock
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        probeMock
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);
        probeMock
            .Setup(expression: p => p.LookupMusicReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (CandidateMatch?)null);

        Mock<IInboxAudioTagReader> tagMock = new();
        tagMock
            .Setup(expression: r =>
                r.ReadAsync(It.IsAny<string>(), It.IsAny<Ulid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: (InboxAudioTags?)null);

        Mock<IStorage> storageMock = new();
        storageMock
            .Setup(expression: s => s.List("", null, false))
            .Returns(value: [new(Path: childFile, IsDirectory: false, SizeBytes: 1024, LastModified: DateTimeOffset.UtcNow)]);
        storageMock
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(valueFunction: (parent, child) => $"{parent}/{child}");
        storageMock
            .Setup(expression: s => s.OpenRead(It.IsAny<string>()))
            .Returns(value: new MemoryStream(buffer: [0x1A, 0x45, 0xDF, 0xA3, 0x00, 0x00, 0x00, 0x00]));

        Mock<IStorageFactory> storageFactoryMock = new();
        storageFactoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: storageMock.Object);

        InboxClassifier classifier = new(probe: probeMock.Object, tagReader: tagMock.Object);
        NoMercy.MediaProcessing.Jobs.JobDispatcher jobDispatcher = new();
        InboxRoutingService routing = new(storageFactory: storageFactoryMock.Object, jobDispatcher: jobDispatcher);

        InboxClassifierEventHandler handler = new(
            logger: NullLogger<InboxClassifierEventHandler>.Instance,
            eventBus: bus,
            classifier: classifier,
            routing: routing,
            contextFactory: CreateContext,
            storageFactory: storageFactoryMock.Object
        );

        return (bus, handler, storageFactoryMock);
    }

    [Fact]
    public async Task FileCreatedEvent_InboxLibrary_PropagatesTo_InboxItemDetectedEvent_ViaRealBus()
    {
        Ulid libraryId = SeedInboxLibrary(folderPath: "/inbox");
        (InMemoryEventBus bus, InboxClassifierEventHandler _, Mock<IStorageFactory> _) =
            BuildChain();

        List<InboxItemDetectedEvent> detected = [];
        bus.Subscribe<InboxItemDetectedEvent>(
            handler: (evt, _) =>
            {
                detected.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new FileCreatedEvent
            {
                FolderPath = "/inbox/item.mkv",
                LibraryId = libraryId,
                LibraryType = MediaTypes.InboxMediaType,
            }
        );

        detected
            .Should()
            .ContainSingle(
                because: "FileCreatedEvent for an inbox library must chain to InboxItemDetectedEvent"
            );
        detected[index: 0].DetectedType.Should().NotBeNullOrEmpty();
        detected[index: 0].Status.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FileCreatedEvent_NonInboxLibrary_DoesNotTriggerInboxClassifier()
    {
        (
            InMemoryEventBus bus,
            InboxClassifierEventHandler _,
            Mock<IStorageFactory> storageFactoryMock
        ) = BuildChain();

        List<InboxItemDetectedEvent> detected = [];
        bus.Subscribe<InboxItemDetectedEvent>(
            handler: (evt, _) =>
            {
                detected.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new FileCreatedEvent
            {
                FolderPath = "/media/movies/Inception (2010)/Inception.mkv",
                LibraryId = Ulid.NewUlid(),
                LibraryType = MediaTypes.MovieMediaType,
            }
        );

        detected
            .Should()
            .BeEmpty(
                because: "InboxClassifierEventHandler ignores non-inbox library events — no InboxItemDetectedEvent must be published"
            );

        storageFactoryMock.Verify(
            expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()),
            times: Times.Never,
            failMessage: "storage was never accessed — the handler exited before any classification attempt"
        );
    }

    [Fact]
    public async Task FileCreatedEvent_InboxLibrary_PublishedEventCarriesCorrectPayload()
    {
        Ulid libraryId = SeedInboxLibrary(folderPath: "/inbox");
        (InMemoryEventBus bus, InboxClassifierEventHandler _, Mock<IStorageFactory> _) =
            BuildChain();

        InboxItemDetectedEvent? captured = null;
        bus.Subscribe<InboxItemDetectedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new FileCreatedEvent
            {
                FolderPath = "/inbox/item.mkv",
                LibraryId = libraryId,
                LibraryType = MediaTypes.InboxMediaType,
            }
        );

        captured.Should().NotBeNull();
        captured!.Id.Should().NotBeNullOrEmpty(because: "event Id must be the saved InboxItem Ulid");
        captured
            .DetectedType.Should()
            .NotBeNullOrEmpty(because: "must have a detected type from the classifier");
        captured.Confidence.Should().NotBeNullOrEmpty();
        captured.Status.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FileCreatedEvent_InboxLibrary_LibraryNotSeeded_NoEventPublished()
    {
        (
            InMemoryEventBus bus,
            InboxClassifierEventHandler _,
            Mock<IStorageFactory> storageFactoryMock
        ) = BuildChain();

        List<InboxItemDetectedEvent> detected = [];
        bus.Subscribe<InboxItemDetectedEvent>(
            handler: (evt, _) =>
            {
                detected.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new FileCreatedEvent
            {
                FolderPath = "/inbox/item.mkv",
                LibraryId = Ulid.NewUlid(),
                LibraryType = MediaTypes.InboxMediaType,
            }
        );

        detected
            .Should()
            .BeEmpty(
                because: "when the library is not in the DB the handler returns early — no event must propagate"
            );
    }
}
