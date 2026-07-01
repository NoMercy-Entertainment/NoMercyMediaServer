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
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.EventHandlers;

/// <summary>
/// Lifecycle + dispatch-leg tests for <see cref="AutoEncodeSubscriber"/>. The
/// subscriber now takes an injected <c>IDbContextFactory</c> and
/// <c>JobDispatcher</c>, so the full scan-to-dispatch chain is assertable
/// without the real queue infrastructure. These tests verify:
/// - Start subscribes
/// - Stop disposes subscriptions
/// - Multiple Start/Stop cycles don't leak subscriptions
/// - A scan event for a profile-mapped folder actually dispatches a
///   VideoEncodeJob (the leg that used to be untestable)
/// - A scan event for a library with no encoder-profile folder dispatches nothing
/// </summary>
public class AutoEncodeSubscriberLifecycleTests
{
    private static IStorage NoOpStorage()
    {
        IStorageDriver driver = new LocalStorageDriver();
        return new LocalStorage(driver, new StoragePathGuard([], driver));
    }

    private static IDbContextFactory<MediaContext> ContextFactory(out SqliteConnection connection)
    {
        SqliteConnection conn = new("DataSource=:memory:");
        conn.Open();
        connection = conn;

        // These tests seed a folder without materialising its Driver row; the
        // subscriber's decision logic doesn't need it, so disable FK enforcement
        // rather than build the full driver graph.
        using (SqliteCommand pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(conn)
            .Options;

        using (MediaContext seed = new(options))
            seed.Database.EnsureCreated();

        Mock<IDbContextFactory<MediaContext>> factory = new();
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MediaContext(options));
        factory.Setup(f => f.CreateDbContext()).Returns(() => new MediaContext(options));
        return factory.Object;
    }

    [Fact]
    public async Task Start_SubscribesToMediaFilesScannedEvent()
    {
        InMemoryEventBus bus = new();
        Mock<JobDispatcher> dispatcher = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            NoOpStorage(),
            ContextFactory(out SqliteConnection connection),
            dispatcher.Object
        );

        await subscriber.StartAsync(CancellationToken.None);

        // Publishing an event while subscribed should reach the handler without
        // throwing. No folders match, so the handler returns early.
        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = -1, LibraryId = Ulid.NewUlid() }
        );

        connection.Dispose();
        Assert.True(true);
    }

    [Fact]
    public async Task Stop_DisposesSubscriptions_EventBusNoLongerCalls()
    {
        TrackingEventBus bus = new();
        Mock<JobDispatcher> dispatcher = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            NoOpStorage(),
            ContextFactory(out SqliteConnection connection),
            dispatcher.Object
        );

        await subscriber.StartAsync(CancellationToken.None);
        Assert.Single(bus.ActiveSubscriptions);

        await subscriber.StopAsync(CancellationToken.None);
        Assert.Empty(bus.ActiveSubscriptions);
        connection.Dispose();
    }

    [Fact]
    public async Task MultipleStartStopCycles_DoNotLeakSubscriptions()
    {
        TrackingEventBus bus = new();
        Mock<JobDispatcher> dispatcher = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            NoOpStorage(),
            ContextFactory(out SqliteConnection connection),
            dispatcher.Object
        );

        for (int i = 0; i < 3; i++)
        {
            await subscriber.StartAsync(CancellationToken.None);
            await subscriber.StopAsync(CancellationToken.None);
        }

        Assert.Empty(bus.ActiveSubscriptions);
        connection.Dispose();
    }

    [Fact]
    public async Task ScanEvent_ForProfileMappedFolderWithVideoFile_DispatchesVideoEncodeJob()
    {
        Ulid libraryId = Ulid.NewUlid();
        Ulid mediaId = Ulid.NewUlid();

        IDbContextFactory<MediaContext> factory = ContextFactory(out SqliteConnection connection);
        int movieId = SeedProfileMappedMovie(factory, libraryId, mediaId);

        Mock<JobDispatcher> dispatcher = new();
        InMemoryEventBus bus = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            NoOpStorage(),
            factory,
            dispatcher.Object
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = movieId, LibraryId = libraryId }
        );

        dispatcher.Verify(
            d =>
                d.DispatchJob<VideoEncodeJob>(
                    libraryId,
                    It.IsAny<Ulid>(),
                    movieId.ToString(),
                    It.Is<string>(path => path.Contains("Movie.mkv"))
                ),
            Times.Once,
            "a scan for a profile-mapped folder must queue exactly one VideoEncodeJob"
        );
        connection.Dispose();
    }

    [Fact]
    public async Task ScanEvent_ForLibraryWithoutEncoderProfileFolder_DispatchesNothing()
    {
        Ulid libraryId = Ulid.NewUlid();

        IDbContextFactory<MediaContext> factory = ContextFactory(out SqliteConnection connection);

        Mock<JobDispatcher> dispatcher = new();
        InMemoryEventBus bus = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            NoOpStorage(),
            factory,
            dispatcher.Object
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(new MediaFilesScannedEvent { MediaId = 1, LibraryId = libraryId });

        dispatcher.Verify(
            d =>
                d.DispatchJob<VideoEncodeJob>(
                    It.IsAny<Ulid>(),
                    It.IsAny<Ulid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()
                ),
            Times.Never,
            "no encoder-profile folder means no auto-encode"
        );
        connection.Dispose();
    }

    private static int SeedProfileMappedMovie(
        IDbContextFactory<MediaContext> factory,
        Ulid libraryId,
        Ulid mediaId
    )
    {
        using MediaContext context = factory.CreateDbContext();

        Ulid driverId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();
        const string folderPath = "/media/movies";

        Library library = new() { Id = libraryId, Title = "Movies" };
        context.Libraries.Add(library);

        Folder folder = new()
        {
            Id = folderId,
            Path = folderPath,
            DriverId = driverId,
        };
        context.Folders.Add(folder);
        context.FolderLibrary.Add(new FolderLibrary { FolderId = folderId, LibraryId = libraryId });

        EncoderProfile profile = new() { Id = Ulid.NewUlid(), Name = "Archive 1080p" };
        context.EncoderProfiles.Add(profile);
        context.EncoderProfileFolder.Add(
            new EncoderProfileFolder { EncoderProfileId = profile.Id, FolderId = folderId }
        );

        int movieId = 4242;
        context.VideoFiles.Add(
            new VideoFile
            {
                Id = Ulid.NewUlid(),
                MovieId = movieId,
                HostFolder = folderPath,
                Filename = "/Movie.mkv",
                Quality = "1080p",
                Share = "local",
                Languages = "eng",
            }
        );

        context.SaveChanges();
        return movieId;
    }

    /// <summary>
    /// Wraps <see cref="InMemoryEventBus"/> to expose a count of live
    /// subscriptions so tests can verify disposal without poking at the bus
    /// internals via reflection.
    /// </summary>
    private sealed class TrackingEventBus : IEventBus
    {
        private readonly InMemoryEventBus _inner = new();
        public List<IDisposable> ActiveSubscriptions { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IEvent => _inner.PublishAsync(@event, ct);

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : IEvent
        {
            IDisposable subscription = _inner.Subscribe(handler);
            TrackingSubscription tracker = new(subscription, this);
            ActiveSubscriptions.Add(tracker);
            return tracker;
        }

        public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : IEvent
        {
            IDisposable subscription = _inner.Subscribe(handler);
            TrackingSubscription tracker = new(subscription, this);
            ActiveSubscriptions.Add(tracker);
            return tracker;
        }

        private sealed class TrackingSubscription(IDisposable inner, TrackingEventBus owner)
            : IDisposable
        {
            public void Dispose()
            {
                inner.Dispose();
                owner.ActiveSubscriptions.Remove(this);
            }
        }
    }
}
