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

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Api.EventHandlers;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Events.Playback;
using NoMercy.Networking.Messaging;
using Xunit;

namespace NoMercy.Tests.Events;

public class SignalREventHandlerTests
{
    private static readonly IClientMessenger NoOpMessenger = new NoOpClientMessenger();

    private sealed class NoOpClientMessenger : IClientMessenger
    {
        public Task SendToAll(string name, string endpoint, object? data = null) =>
            Task.CompletedTask;

        public Task SendTo(string name, string endpoint, Guid userId, object? data = null) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task PlaybackHandler_SubscribesToAllPlaybackEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<PlaybackStartedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<PlaybackProgressUpdatedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<PlaybackCompletedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        using SignalRPlaybackEventHandler handler = new(
            logger: NullLogger<SignalRPlaybackEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 1,
                MediaType = "movie",
                DeviceId = "d1",
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 1,
                Position = TimeSpan.FromMinutes(minutes: 10),
                Duration = TimeSpan.FromMinutes(minutes: 120),
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 1,
                MediaType = "movie",
            }
        );

        // The handler subscriptions + our test subscriptions = events delivered to both
        received.Should().HaveCount(expected: 3);
    }

    [Fact]
    public async Task PlaybackHandler_Dispose_UnsubscribesFromEvents()
    {
        InMemoryEventBus bus = new();
        int handlerCallCount = 0;

        // Wrap the handler to track invocations via a separate subscriber
        bus.Subscribe<PlaybackStartedEvent>(
            handler: (_, _) =>
            {
                Interlocked.Increment(location: ref handlerCallCount);
                return Task.CompletedTask;
            }
        );

        SignalRPlaybackEventHandler handler = new(
            logger: NullLogger<SignalRPlaybackEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            }
        );

        int countBeforeDispose = handlerCallCount;
        handler.Dispose();

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 2,
                MediaType = "tv",
            }
        );

        // Our own test subscriber is still active, so count increases by 1
        // but handler's internal subscriptions are gone
        handlerCallCount.Should().Be(expected: countBeforeDispose + 1);
    }

    [Fact]
    public async Task EncodingHandler_SubscribesToAllEncodingEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<EncodingStartedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<EncodingProgressUpdatedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<EncodingCompletedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<EncodingFailedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        using SignalREncodingEventHandler handler = new(
            logger: NullLogger<SignalREncodingEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/input.mkv",
                OutputPath = "/output/",
                ProfileName = "x264",
            }
        );

        await bus.PublishAsync(
            @event: new EncodingProgressUpdatedEvent
            {
                JobId = 1,
                Percentage = 50.0,
                Elapsed = TimeSpan.FromMinutes(minutes: 5),
            }
        );

        await bus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/output/playlist.m3u8",
                Duration = TimeSpan.FromMinutes(minutes: 10),
            }
        );

        await bus.PublishAsync(
            @event: new EncodingFailedEvent
            {
                JobId = 2,
                InputPath = "/bad.mkv",
                ErrorMessage = "Invalid codec",
            }
        );

        received.Should().HaveCount(expected: 4);
        received[index: 0].Should().BeOfType<EncodingStartedEvent>();
        received[index: 1].Should().BeOfType<EncodingProgressUpdatedEvent>();
        received[index: 2].Should().BeOfType<EncodingCompletedEvent>();
        received[index: 3].Should().BeOfType<EncodingFailedEvent>();
    }

    [Fact]
    public async Task EncodingHandler_BroadcastsToSignalR_WithoutException()
    {
        InMemoryEventBus bus = new();
        using SignalREncodingEventHandler handler = new(
            logger: NullLogger<SignalREncodingEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        // SendToAll will find no connected clients and silently succeed
        Func<Task> act = () =>
            bus.PublishAsync(
                @event: new EncodingStartedEvent
                {
                    JobId = 1,
                    InputPath = "/input.mkv",
                    OutputPath = "/output/",
                    ProfileName = "x264",
                }
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryScanHandler_SubscribesToAllLibraryEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryScanStartedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<LibraryScanCompletedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<MediaAddedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<MediaRemovedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        using SignalRLibraryScanEventHandler handler = new(
            logger: NullLogger<SignalRLibraryScanEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        Ulid libraryId = Ulid.NewUlid();

        await bus.PublishAsync(
            @event: new LibraryScanStartedEvent { LibraryId = libraryId, LibraryName = "Movies" }
        );

        await bus.PublishAsync(
            @event: new MediaAddedEvent
            {
                MediaId = 42,
                MediaType = "movie",
                Title = "Test Movie",
                LibraryId = libraryId,
            }
        );

        await bus.PublishAsync(
            @event: new MediaRemovedEvent
            {
                MediaId = 99,
                MediaType = "movie",
                Title = "Old Movie",
                LibraryId = libraryId,
            }
        );

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "Movies",
                ItemsFound = 42,
                Duration = TimeSpan.FromSeconds(seconds: 30),
            }
        );

        received.Should().HaveCount(expected: 4);
        received[index: 0].Should().BeOfType<LibraryScanStartedEvent>();
        received[index: 1].Should().BeOfType<MediaAddedEvent>();
        received[index: 2].Should().BeOfType<MediaRemovedEvent>();
        received[index: 3].Should().BeOfType<LibraryScanCompletedEvent>();
    }

    [Fact]
    public async Task LibraryScanHandler_BroadcastsToSignalR_WithoutException()
    {
        InMemoryEventBus bus = new();
        using SignalRLibraryScanEventHandler handler = new(
            logger: NullLogger<SignalRLibraryScanEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        Func<Task> act = () =>
            bus.PublishAsync(
                @event: new LibraryScanStartedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "TV Shows" }
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EncodingHandler_Dispose_UnsubscribesFromEvents()
    {
        InMemoryEventBus bus = new();
        int externalCount = 0;

        bus.Subscribe<EncodingStartedEvent>(
            handler: (_, _) =>
            {
                Interlocked.Increment(location: ref externalCount);
                return Task.CompletedTask;
            }
        );

        SignalREncodingEventHandler handler = new(
            logger: NullLogger<SignalREncodingEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/a.mkv",
                OutputPath = "/out/",
                ProfileName = "x264",
            }
        );

        int countBefore = externalCount;
        handler.Dispose();

        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 2,
                InputPath = "/b.mkv",
                OutputPath = "/out/",
                ProfileName = "x265",
            }
        );

        // External subscriber still fires
        externalCount.Should().Be(expected: countBefore + 1);
    }

    [Fact]
    public async Task AllHandlers_CanCoexistOnSameBus()
    {
        InMemoryEventBus bus = new();

        using SignalRPlaybackEventHandler playbackHandler = new(
            logger: NullLogger<SignalRPlaybackEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );
        using SignalREncodingEventHandler encodingHandler = new(
            logger: NullLogger<SignalREncodingEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );
        using SignalRLibraryScanEventHandler libraryScanHandler = new(
            logger: NullLogger<SignalRLibraryScanEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        // Publish one event of each type - no cross-talk or exceptions
        Func<Task> act = async () =>
        {
            await bus.PublishAsync(
                @event: new PlaybackStartedEvent
                {
                    UserId = Guid.NewGuid(),
                    MediaId = 1,
                    MediaType = "movie",
                }
            );

            await bus.PublishAsync(
                @event: new EncodingStartedEvent
                {
                    JobId = 1,
                    InputPath = "/a.mkv",
                    OutputPath = "/out/",
                    ProfileName = "x264",
                }
            );

            await bus.PublishAsync(
                @event: new LibraryScanStartedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "Movies" }
            );
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PlaybackHandler_OnPlaybackStarted_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalRPlaybackEventHandler handler = new(
            logger: NullLogger<SignalRPlaybackEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        Func<Task> act = () =>
            handler.OnPlaybackStarted(
                @event: new()
                {
                    UserId = Guid.NewGuid(),
                    MediaId = 129,
                    MediaType = "movie",
                    DeviceId = "dev-1",
                },
                ct: CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PlaybackHandler_OnPlaybackCompleted_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalRPlaybackEventHandler handler = new(
            logger: NullLogger<SignalRPlaybackEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        Func<Task> act = () =>
            handler.OnPlaybackCompleted(
                @event: new()
                {
                    UserId = Guid.NewGuid(),
                    MediaId = 129,
                    MediaType = "movie",
                },
                ct: CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EncodingHandler_OnEncodingProgress_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalREncodingEventHandler handler = new(
            logger: NullLogger<SignalREncodingEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );

        Func<Task> act = () =>
            handler.OnEncodingProgress(
                @event: new()
                {
                    JobId = 1,
                    Percentage = 75.5,
                    Elapsed = TimeSpan.FromMinutes(minutes: 3),
                    Estimated = TimeSpan.FromMinutes(minutes: 1),
                },
                ct: CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryRefreshHandler_SubscribesToLibraryRefreshEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryRefreshedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        using SignalRLibraryRefreshEventHandler handler = new(eventBus: bus, clientMessenger: NoOpMessenger);

        await bus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "album", Guid.NewGuid()] }
        );

        await bus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["libraries", Ulid.NewUlid().ToString()] }
        );

        await bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["home"] });

        received.Should().HaveCount(expected: 3);
        received.Should().AllBeOfType<LibraryRefreshedEvent>();
    }

    [Fact]
    public async Task LibraryRefreshHandler_BroadcastsToSignalR_WithoutException()
    {
        InMemoryEventBus bus = new();
        using SignalRLibraryRefreshEventHandler handler = new(eventBus: bus, clientMessenger: NoOpMessenger);

        Func<Task> act = () =>
            bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["music", "tracks"] });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryRefreshHandler_Dispose_UnsubscribesFromEvents()
    {
        InMemoryEventBus bus = new();
        int externalCount = 0;

        bus.Subscribe<LibraryRefreshedEvent>(
            handler: (_, _) =>
            {
                Interlocked.Increment(location: ref externalCount);
                return Task.CompletedTask;
            }
        );

        SignalRLibraryRefreshEventHandler handler = new(eventBus: bus, clientMessenger: NoOpMessenger);

        await bus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "album", Guid.NewGuid()] }
        );

        int countBefore = externalCount;
        handler.Dispose();

        await bus.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "artist", Guid.NewGuid()] }
        );

        // External subscriber still fires
        externalCount.Should().Be(expected: countBefore + 1);
    }

    [Fact]
    public async Task LibraryRefreshHandler_OnLibraryRefresh_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalRLibraryRefreshEventHandler handler = new(eventBus: bus, clientMessenger: NoOpMessenger);

        Func<Task> act = () =>
            handler.OnLibraryRefresh(
                @event: new() { QueryKey = ["base", "info", "123"] },
                ct: CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AllHandlers_IncludingRefresh_CanCoexistOnSameBus()
    {
        InMemoryEventBus bus = new();

        using SignalRPlaybackEventHandler playbackHandler = new(
            logger: NullLogger<SignalRPlaybackEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );
        using SignalREncodingEventHandler encodingHandler = new(
            logger: NullLogger<SignalREncodingEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );
        using SignalRLibraryScanEventHandler libraryScanHandler = new(
            logger: NullLogger<SignalRLibraryScanEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: NoOpMessenger
        );
        using SignalRLibraryRefreshEventHandler libraryRefreshHandler = new(eventBus: bus, clientMessenger: NoOpMessenger);

        Func<Task> act = async () =>
        {
            await bus.PublishAsync(
                @event: new PlaybackStartedEvent
                {
                    UserId = Guid.NewGuid(),
                    MediaId = 1,
                    MediaType = "movie",
                }
            );

            await bus.PublishAsync(
                @event: new EncodingStartedEvent
                {
                    JobId = 1,
                    InputPath = "/a.mkv",
                    OutputPath = "/out/",
                    ProfileName = "x264",
                }
            );

            await bus.PublishAsync(
                @event: new LibraryScanStartedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "Movies" }
            );

            await bus.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["music", "playlists", Guid.NewGuid()] }
            );
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryRefreshEvent_PreservesQueryKey()
    {
        InMemoryEventBus bus = new();
        LibraryRefreshedEvent? capturedEvent = null;

        bus.Subscribe<LibraryRefreshedEvent>(
            handler: (evt, _) =>
            {
                capturedEvent = evt;
                return Task.CompletedTask;
            }
        );

        dynamic?[] queryKey = ["music", "album", Guid.NewGuid()];

        await bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = queryKey });

        capturedEvent.Should().NotBeNull();
        capturedEvent!.QueryKey.Should().BeEquivalentTo(expectation: queryKey);
        capturedEvent.Source.Should().Be(expected: "LibraryRefresh");
        capturedEvent.EventId.Should().NotBeEmpty();
        capturedEvent.Timestamp.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 5));
    }
}
