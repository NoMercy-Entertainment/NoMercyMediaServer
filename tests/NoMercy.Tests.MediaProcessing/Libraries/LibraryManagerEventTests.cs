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
using NoMercy.Encoder.Analysis;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Factory;

namespace NoMercy.Tests.MediaProcessing.Libraries;

public class LibraryManagerEventTests : IDisposable
{
    private static readonly IMediaAnalyzer MediaAnalyzer = new Mock<IMediaAnalyzer>().Object;

    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;

    public LibraryManagerEventTests()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new(connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();
        _connection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection: _connection,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;

        _context = new(options: options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessLibrary_NonExistentLibrary_DoesNotPublishEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryScanStartedEvent>(
            handler: (e, _) =>
            {
                received.Add(item: e);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<LibraryScanCompletedEvent>(
            handler: (e, _) =>
            {
                received.Add(item: e);
                return Task.CompletedTask;
            }
        );

        IStorageDriver driver = new LocalStorageDriver();
        StorageFactory storageFactory = new(driver: driver, logger: NullLogger<StorageFactory>.Instance);
        LibraryRepository repo = new(context: _context, storageDriver: driver);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(
            libraryRepository: repo,
            jobDispatcher: dispatcher,
            mediaContext: _context,
            storageDriver: driver,
            storageFactory: storageFactory,
            mediaAnalyzer: MediaAnalyzer,
            logger: NullLogger<LibraryManager>.Instance,
            eventBus: bus
        );

        await manager.ProcessLibrary(id: Ulid.NewUlid());

        Assert.Empty(collection: received);
    }

    [Fact]
    public async Task ProcessLibrary_EmptyLibrary_PublishesStartAndCompletedEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryScanStartedEvent>(
            handler: (e, _) =>
            {
                received.Add(item: e);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<LibraryScanCompletedEvent>(
            handler: (e, _) =>
            {
                received.Add(item: e);
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Test Movies",
                Type = "movie",
            }
        );
        await _context.SaveChangesAsync();

        IStorageDriver driver = new LocalStorageDriver();
        StorageFactory storageFactory = new(driver: driver, logger: NullLogger<StorageFactory>.Instance);
        LibraryRepository repo = new(context: _context, storageDriver: driver);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(
            libraryRepository: repo,
            jobDispatcher: dispatcher,
            mediaContext: _context,
            storageDriver: driver,
            storageFactory: storageFactory,
            mediaAnalyzer: MediaAnalyzer,
            logger: NullLogger<LibraryManager>.Instance,
            eventBus: bus
        );

        await manager.ProcessLibrary(id: libraryId);

        Assert.Equal(expected: 2, actual: received.Count);

        LibraryScanStartedEvent started = Assert.IsType<LibraryScanStartedEvent>(@object: received[index: 0]);
        Assert.Equal(expected: libraryId, actual: started.LibraryId);
        Assert.Equal(expected: "Test Movies", actual: started.LibraryName);

        LibraryScanCompletedEvent completed = Assert.IsType<LibraryScanCompletedEvent>(@object: received[index: 1]);
        Assert.Equal(expected: libraryId, actual: completed.LibraryId);
        Assert.Equal(expected: "Test Movies", actual: completed.LibraryName);
        Assert.Equal(expected: 0, actual: completed.ItemsFound);
        Assert.True(condition: completed.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessLibrary_WithoutEventBus_DoesNotThrow()
    {
        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "No Events Library",
                Type = "movie",
            }
        );
        await _context.SaveChangesAsync();

        IStorageDriver driver = new LocalStorageDriver();
        StorageFactory storageFactory = new(driver: driver, logger: NullLogger<StorageFactory>.Instance);
        LibraryRepository repo = new(context: _context, storageDriver: driver);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(
            libraryRepository: repo,
            jobDispatcher: dispatcher,
            mediaContext: _context,
            storageDriver: driver,
            storageFactory: storageFactory,
            mediaAnalyzer: MediaAnalyzer,
            logger: NullLogger<LibraryManager>.Instance
        );

        await manager.ProcessLibrary(id: libraryId);
    }

    [Fact]
    public async Task ProcessLibrary_CompletedEvent_HasValidDuration()
    {
        InMemoryEventBus bus = new();
        LibraryScanCompletedEvent? completedEvent = null;

        bus.Subscribe<LibraryScanCompletedEvent>(
            handler: (e, _) =>
            {
                completedEvent = e;
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Duration Test",
                Type = "tv",
            }
        );
        await _context.SaveChangesAsync();

        IStorageDriver driver = new LocalStorageDriver();
        StorageFactory storageFactory = new(driver: driver, logger: NullLogger<StorageFactory>.Instance);
        LibraryRepository repo = new(context: _context, storageDriver: driver);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(
            libraryRepository: repo,
            jobDispatcher: dispatcher,
            mediaContext: _context,
            storageDriver: driver,
            storageFactory: storageFactory,
            mediaAnalyzer: MediaAnalyzer,
            logger: NullLogger<LibraryManager>.Instance,
            eventBus: bus
        );

        await manager.ProcessLibrary(id: libraryId);

        Assert.NotNull(@object: completedEvent);
        Assert.True(condition: completedEvent.Duration >= TimeSpan.Zero);
        Assert.True(condition: completedEvent.Duration < TimeSpan.FromSeconds(seconds: 10));
    }

    [Fact]
    public async Task ProcessLibrary_StartedEvent_HasCorrectEventMetadata()
    {
        InMemoryEventBus bus = new();
        LibraryScanStartedEvent? startedEvent = null;

        bus.Subscribe<LibraryScanStartedEvent>(
            handler: (e, _) =>
            {
                startedEvent = e;
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Metadata Test",
                Type = "movie",
            }
        );
        await _context.SaveChangesAsync();

        IStorageDriver driver = new LocalStorageDriver();
        StorageFactory storageFactory = new(driver: driver, logger: NullLogger<StorageFactory>.Instance);
        LibraryRepository repo = new(context: _context, storageDriver: driver);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(
            libraryRepository: repo,
            jobDispatcher: dispatcher,
            mediaContext: _context,
            storageDriver: driver,
            storageFactory: storageFactory,
            mediaAnalyzer: MediaAnalyzer,
            logger: NullLogger<LibraryManager>.Instance,
            eventBus: bus
        );

        await manager.ProcessLibrary(id: libraryId);

        Assert.NotNull(@object: startedEvent);
        Assert.NotEqual(expected: Guid.Empty, actual: startedEvent.EventId);
        Assert.True(condition: startedEvent.Timestamp <= DateTime.UtcNow);
        Assert.Equal(expected: "LibraryScanner", actual: startedEvent.Source);
    }
}
