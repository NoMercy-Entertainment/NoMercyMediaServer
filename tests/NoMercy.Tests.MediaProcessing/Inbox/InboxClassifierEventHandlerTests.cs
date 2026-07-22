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
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Events.Inbox;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.MediaProcessing.Inbox;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.NmSystem.Domain;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Factory;

namespace NoMercy.Tests.MediaProcessing.Inbox;

[Trait(name: "Category", value: "Unit")]
public class InboxClassifierEventHandlerTests : IDisposable
{
    // -----------------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------------

    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<IInboxMetadataProbe> _probeMock;
    private readonly Mock<IInboxAudioTagReader> _tagReaderMock;
    private readonly Mock<IStorageFactory> _storageFactoryMock;

    public InboxClassifierEventHandlerTests()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new(connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;

        _context = new(options: options);
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw(sql: "PRAGMA foreign_keys = OFF;");

        _eventBusMock = new();
        _eventBusMock
            .Setup(expression: bus =>
                bus.Subscribe<FileCreatedEvent>(
                    It.IsAny<Func<FileCreatedEvent, CancellationToken, Task>>()
                )
            )
            .Returns(value: Mock.Of<IDisposable>());
        _eventBusMock
            .Setup(expression: bus =>
                bus.PublishAsync(It.IsAny<InboxItemDetectedEvent>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);

        _probeMock = new();
        _probeMock
            .Setup(expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);
        _probeMock
            .Setup(expression: p =>
                p.SearchTvAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: []);
        _probeMock
            .Setup(expression: p => p.LookupMusicReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (CandidateMatch?)null);

        _tagReaderMock = new();
        _tagReaderMock
            .Setup(expression: r =>
                r.ReadAsync(It.IsAny<string>(), It.IsAny<Ulid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: (InboxAudioTags?)null);

        _storageFactoryMock = new();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private MediaContext CreateSharedContext()
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;
        return new(options: options);
    }

    private InboxClassifierEventHandler MakeHandler()
    {
        InboxClassifier classifier = new(probe: _probeMock.Object, tagReader: _tagReaderMock.Object);
        InboxRoutingService routing = new(storageFactory: _storageFactoryMock.Object, jobDispatcher: new());

        return new(
            logger: NullLogger<InboxClassifierEventHandler>.Instance,
            eventBus: _eventBusMock.Object,
            classifier: classifier,
            routing: routing,
            contextFactory: CreateSharedContext,
            storageFactory: _storageFactoryMock.Object
        );
    }

    private Ulid SeedInboxLibraryWithFolder(string folderPath)
    {
        Ulid libraryId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        _context.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Inbox",
                Type = MediaTypes.InboxMediaType,
            }
        );

        _context.Folders.Add(
            entity: new()
            {
                Id = folderId,
                Path = folderPath,
                DriverId = driverId,
            }
        );

        _context.FolderLibrary.Add(entity: new() { LibraryId = libraryId, FolderId = folderId });

        _context.SaveChanges();

        return libraryId;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NonInboxLibraryType_IsIgnored_ClassifyNeverCalled()
    {
        InboxClassifierEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = "/media/movies/Inception (2010)/Inception.mkv",
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.MovieMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _probeMock.Verify(
            expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
        _probeMock.Verify(
            expression: p =>
                p.SearchTvAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task InboxEvent_LibraryNotFound_ReturnsWithoutClassify()
    {
        InboxClassifierEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = "/inbox/somefile.mkv",
            LibraryId = Ulid.NewUlid(),
            LibraryType = MediaTypes.InboxMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        _probeMock.Verify(
            expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task InboxEvent_WithSeededLibrary_TriggersClassifyAndRoute()
    {
        Ulid libraryId = SeedInboxLibraryWithFolder(folderPath: "/inbox");

        // Storage mock returns one .mkv child
        Mock<IStorage> storageMock = new();
        storageMock
            .Setup(expression: s => s.List("", null, false))
            .Returns(value: [new(Path: "somefile.mkv", IsDirectory: false, SizeBytes: 1024, LastModified: DateTimeOffset.UtcNow)]);
        storageMock
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(valueFunction: (parent, child) => $"{parent}/{child}");
        _storageFactoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: storageMock.Object);

        List<InboxItemDetectedEvent> published = [];
        _eventBusMock
            .Setup(expression: bus =>
                bus.PublishAsync(It.IsAny<InboxItemDetectedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<InboxItemDetectedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        InboxClassifierEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = "/inbox/somefile.mkv",
            LibraryId = libraryId,
            LibraryType = MediaTypes.InboxMediaType,
        };

        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        // .mkv is a video extension → probe is called for movie/tv
        _probeMock.Verify(
            expression: p =>
                p.SearchMoviesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.AtLeastOnce
        );

        published.Should().ContainSingle();
        published[index: 0].Status.Should().Be(expected: "NeedsReview");
        published[index: 0].DetectedType.Should().NotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Integration test: loose dropped file classified by real extension
    // -----------------------------------------------------------------------

    [Trait(name: "Category", value: "Integration")]
    [Fact]
    public async Task LooseDroppedFile_IsClassifiedByExtension_NotInboxDir()
    {
        // Arrange: real temp inbox dir with a real .mkv file
        string tempRoot = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nm-inbox-handler-it-" + Path.GetRandomFileName()
        );
        string inboxDir = Path.Combine(path1: tempRoot, path2: "inbox");
        Directory.CreateDirectory(path: inboxDir);

        string droppedFilePath = Path.Combine(path1: inboxDir, path2: "The Matrix (1999).mkv");
        await File.WriteAllBytesAsync(path: droppedFilePath, bytes: [0x1A, 0x45, 0xDF, 0xA3]);

        try
        {
            // Real storage factory scoped to the inbox dir
            LocalStorageDriver realDriver = new();
            Mock<IDriverConfigResolver> resolverMock = new();
            resolverMock
                .Setup(expression: r => r.Resolve(It.IsAny<Ulid>()))
                .Returns(value: ("local", "{\"rootPath\":\"\"}"));

            StorageFactory realStorageFactory = new(
                driver: realDriver,
                logger: NullLogger<StorageFactory>.Instance,
                driverConfigResolver: resolverMock.Object
            );

            // Seed inbox library pointing at the real temp inbox dir
            string dbName = Guid.NewGuid().ToString();
            SqliteConnection connection = new(connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared");
            connection.Open();

            DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
                .UseSqlite(connection: connection)
                .Options;

            await using MediaContext seedContext = new(options: options);
            seedContext.Database.EnsureCreated();
            seedContext.Database.ExecuteSqlRaw(sql: "PRAGMA foreign_keys = OFF;");

            Ulid libraryId = Ulid.NewUlid();
            Ulid folderId = Ulid.NewUlid();
            Ulid driverId = Ulid.NewUlid();

            seedContext.Libraries.Add(
                entity: new()
                {
                    Id = libraryId,
                    Title = "Inbox",
                    Type = MediaTypes.InboxMediaType,
                }
            );
            seedContext.Folders.Add(
                entity: new()
                {
                    Id = folderId,
                    Path = inboxDir,
                    DriverId = driverId,
                }
            );
            seedContext.FolderLibrary.Add(entity: new() { LibraryId = libraryId, FolderId = folderId });
            await seedContext.SaveChangesAsync();

            Mock<IEventBus> eventBusMock = new();
            eventBusMock
                .Setup(expression: bus =>
                    bus.Subscribe<FileCreatedEvent>(
                        It.IsAny<Func<FileCreatedEvent, CancellationToken, Task>>()
                    )
                )
                .Returns(value: Mock.Of<IDisposable>());

            List<InboxItemDetectedEvent> published = [];
            eventBusMock
                .Setup(expression: bus =>
                    bus.PublishAsync(
                        It.IsAny<InboxItemDetectedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<InboxItemDetectedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
                .Returns(value: Task.CompletedTask);

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
                    p.SearchTvAsync(
                        It.IsAny<string>(),
                        It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: []);
            probeMock
                .Setup(expression: p =>
                    p.LookupMusicReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(value: (CandidateMatch?)null);

            Mock<IInboxAudioTagReader> tagReaderMock = new();
            tagReaderMock
                .Setup(expression: r =>
                    r.ReadAsync(It.IsAny<string>(), It.IsAny<Ulid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(value: (InboxAudioTags?)null);

            InboxClassifier classifier = new(probe: probeMock.Object, tagReader: tagReaderMock.Object);
            InboxRoutingService routing = new(storageFactory: realStorageFactory, jobDispatcher: new());

            MediaContext ContextFactory()
            {
                DbContextOptions<MediaContext> ctx = new DbContextOptionsBuilder<MediaContext>()
                    .UseSqlite(connection: connection)
                    .Options;
                return new(options: ctx);
            }

            InboxClassifierEventHandler handler = new(
                logger: NullLogger<InboxClassifierEventHandler>.Instance,
                eventBus: eventBusMock.Object,
                classifier: classifier,
                routing: routing,
                contextFactory: ContextFactory,
                storageFactory: realStorageFactory
            );

            FileCreatedEvent @event = new()
            {
                FolderPath = inboxDir,
                LibraryId = libraryId,
                LibraryType = MediaTypes.InboxMediaType,
            };

            // Act
            await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

            // Assert: one InboxItemDetectedEvent published
            published.Should().ContainSingle(because: "one item dropped, one event expected");

            // Assert: detected type is NOT "unknown" — real extension was seen
            published[index: 0].DetectedType.Should().NotBe(unexpected: "unknown");

            // Assert: an InboxItem was saved with SourcePath = the FILE path (not the dir)
            await using MediaContext verifyContext = ContextFactory();
            List<InboxItem> items = await verifyContext.InboxItems.ToListAsync();
            items.Should().ContainSingle();
            string normalizedSource = Path.GetFullPath(
                path: items[index: 0].SourcePath.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar)
            );
            normalizedSource
                .Should()
                .Be(expected: droppedFilePath, because: "SourcePath must be the FILE path, not the inbox directory");
            normalizedSource
                .Should()
                .NotBe(unexpected: inboxDir, because: "SourcePath must not be the inbox root directory");

            connection.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(path: tempRoot, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    // -----------------------------------------------------------------------
    // Dedup: second fire does NOT create a duplicate InboxItem
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SecondFireForSameChild_DoesNotCreateDuplicateInboxItem()
    {
        Ulid libraryId = SeedInboxLibraryWithFolder(folderPath: "/inbox");

        Mock<IStorage> storageMock = new();
        storageMock
            .Setup(expression: s => s.List("", null, false))
            .Returns(value: [new(Path: "The Matrix (1999).mkv", IsDirectory: false, SizeBytes: 1024, LastModified: DateTimeOffset.UtcNow)]);
        storageMock
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(valueFunction: (parent, child) => $"{parent}/{child}");
        _storageFactoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: storageMock.Object);

        List<InboxItemDetectedEvent> published = [];
        _eventBusMock
            .Setup(expression: bus =>
                bus.PublishAsync(It.IsAny<InboxItemDetectedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<InboxItemDetectedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        InboxClassifierEventHandler handler = MakeHandler();

        FileCreatedEvent @event = new()
        {
            FolderPath = "/inbox/The Matrix (1999).mkv",
            LibraryId = libraryId,
            LibraryType = MediaTypes.InboxMediaType,
        };

        // First fire
        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        // Second fire (watcher debounce re-fire)
        await handler.OnFileCreated(@event: @event, ct: CancellationToken.None);

        // Only one InboxItem in DB
        List<InboxItem> items = await _context.InboxItems.ToListAsync();
        items.Should().ContainSingle(because: "second fire must be deduped");

        // Only one event published
        published.Should().ContainSingle(because: "event published only once");
    }
}
