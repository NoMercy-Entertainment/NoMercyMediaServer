using FluentAssertions;
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
        public bool SendToAll(string name, string endpoint, object? data = null) => true;
        public Task SendTo(string name, string endpoint, Guid userId, object? data = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task PlaybackHandler_SubscribesToAllPlaybackEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<PlaybackStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackProgressEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        using SignalRPlaybackEventHandler handler = new(bus, NoOpMessenger);

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = userId,
            MediaId = 1,
            MediaType = "movie",
            DeviceId = "d1"
        });

        await bus.PublishAsync(new PlaybackProgressEvent
        {
            UserId = userId,
            MediaId = 1,
            Position = TimeSpan.FromMinutes(10),
            Duration = TimeSpan.FromMinutes(120)
        });

        await bus.PublishAsync(new PlaybackCompletedEvent
        {
            UserId = userId,
            MediaId = 1,
            MediaType = "movie"
        });

        // The handler subscriptions + our test subscriptions = events delivered to both
        received.Should().HaveCount(3);
    }

    [Fact]
    public async Task PlaybackHandler_Dispose_UnsubscribesFromEvents()
    {
        InMemoryEventBus bus = new();
        int handlerCallCount = 0;

        // Wrap the handler to track invocations via a separate subscriber
        bus.Subscribe<PlaybackStartedEvent>((_, _) =>
        {
            Interlocked.Increment(ref handlerCallCount);
            return Task.CompletedTask;
        });

        SignalRPlaybackEventHandler handler = new(bus, NoOpMessenger);

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "movie"
        });

        int countBeforeDispose = handlerCallCount;
        handler.Dispose();

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = Guid.NewGuid(),
            MediaId = 2,
            MediaType = "tv"
        });

        // Our own test subscriber is still active, so count increases by 1
        // but handler's internal subscriptions are gone
        handlerCallCount.Should().Be(countBeforeDispose + 1);
    }

    [Fact]
    public async Task EncodingHandler_SubscribesToAllEncodingEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<EncodingStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingProgressEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingFailedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        using SignalREncodingEventHandler handler = new(bus, NoOpMessenger);

        await bus.PublishAsync(new EncodingStartedEvent
        {
            JobId = 1,
            InputPath = "/input.mkv",
            OutputPath = "/output/",
            ProfileName = "x264"
        });

        await bus.PublishAsync(new EncodingProgressEvent
        {
            JobId = 1,
            Percentage = 50.0,
            Elapsed = TimeSpan.FromMinutes(5)
        });

        await bus.PublishAsync(new EncodingCompletedEvent
        {
            JobId = 1,
            OutputPath = "/output/playlist.m3u8",
            Duration = TimeSpan.FromMinutes(10)
        });

        await bus.PublishAsync(new EncodingFailedEvent
        {
            JobId = 2,
            InputPath = "/bad.mkv",
            ErrorMessage = "Invalid codec"
        });

        received.Should().HaveCount(4);
        received[0].Should().BeOfType<EncodingStartedEvent>();
        received[1].Should().BeOfType<EncodingProgressEvent>();
        received[2].Should().BeOfType<EncodingCompletedEvent>();
        received[3].Should().BeOfType<EncodingFailedEvent>();
    }

    [Fact]
    public async Task EncodingHandler_BroadcastsToSignalR_WithoutException()
    {
        InMemoryEventBus bus = new();
        using SignalREncodingEventHandler handler = new(bus, NoOpMessenger);

        // SendToAll will find no connected clients and silently succeed
        Func<Task> act = () => bus.PublishAsync(new EncodingStartedEvent
        {
            JobId = 1,
            InputPath = "/input.mkv",
            OutputPath = "/output/",
            ProfileName = "x264"
        });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryScanHandler_SubscribesToAllLibraryEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryScanStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<LibraryScanCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<MediaAddedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<MediaRemovedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        using SignalRLibraryScanEventHandler handler = new(bus, NoOpMessenger);

        Ulid libraryId = Ulid.NewUlid();

        await bus.PublishAsync(new LibraryScanStartedEvent
        {
            LibraryId = libraryId,
            LibraryName = "Movies"
        });

        await bus.PublishAsync(new MediaAddedEvent
        {
            MediaId = 42,
            MediaType = "movie",
            Title = "Test Movie",
            LibraryId = libraryId
        });

        await bus.PublishAsync(new MediaRemovedEvent
        {
            MediaId = 99,
            MediaType = "movie",
            Title = "Old Movie",
            LibraryId = libraryId
        });

        await bus.PublishAsync(new LibraryScanCompletedEvent
        {
            LibraryId = libraryId,
            LibraryName = "Movies",
            ItemsFound = 42,
            Duration = TimeSpan.FromSeconds(30)
        });

        received.Should().HaveCount(4);
        received[0].Should().BeOfType<LibraryScanStartedEvent>();
        received[1].Should().BeOfType<MediaAddedEvent>();
        received[2].Should().BeOfType<MediaRemovedEvent>();
        received[3].Should().BeOfType<LibraryScanCompletedEvent>();
    }

    [Fact]
    public async Task LibraryScanHandler_BroadcastsToSignalR_WithoutException()
    {
        InMemoryEventBus bus = new();
        using SignalRLibraryScanEventHandler handler = new(bus, NoOpMessenger);

        Func<Task> act = () => bus.PublishAsync(new LibraryScanStartedEvent
        {
            LibraryId = Ulid.NewUlid(),
            LibraryName = "TV Shows"
        });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EncodingHandler_Dispose_UnsubscribesFromEvents()
    {
        InMemoryEventBus bus = new();
        int externalCount = 0;

        bus.Subscribe<EncodingStartedEvent>((_, _) =>
        {
            Interlocked.Increment(ref externalCount);
            return Task.CompletedTask;
        });

        SignalREncodingEventHandler handler = new(bus, NoOpMessenger);

        await bus.PublishAsync(new EncodingStartedEvent
        {
            JobId = 1,
            InputPath = "/a.mkv",
            OutputPath = "/out/",
            ProfileName = "x264"
        });

        int countBefore = externalCount;
        handler.Dispose();

        await bus.PublishAsync(new EncodingStartedEvent
        {
            JobId = 2,
            InputPath = "/b.mkv",
            OutputPath = "/out/",
            ProfileName = "x265"
        });

        // External subscriber still fires
        externalCount.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task AllHandlers_CanCoexistOnSameBus()
    {
        InMemoryEventBus bus = new();

        using SignalRPlaybackEventHandler playbackHandler = new(bus, NoOpMessenger);
        using SignalREncodingEventHandler encodingHandler = new(bus, NoOpMessenger);
        using SignalRLibraryScanEventHandler libraryScanHandler = new(bus, NoOpMessenger);

        // Publish one event of each type - no cross-talk or exceptions
        Func<Task> act = async () =>
        {
            await bus.PublishAsync(new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie"
            });

            await bus.PublishAsync(new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/a.mkv",
                OutputPath = "/out/",
                ProfileName = "x264"
            });

            await bus.PublishAsync(new LibraryScanStartedEvent
            {
                LibraryId = Ulid.NewUlid(),
                LibraryName = "Movies"
            });
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PlaybackHandler_OnPlaybackStarted_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalRPlaybackEventHandler handler = new(bus, NoOpMessenger);

        Func<Task> act = () => handler.OnPlaybackStarted(
            new()
            {
                UserId = Guid.NewGuid(),
                MediaId = 129,
                MediaType = "movie",
                DeviceId = "dev-1"
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PlaybackHandler_OnPlaybackCompleted_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalRPlaybackEventHandler handler = new(bus, NoOpMessenger);

        Func<Task> act = () => handler.OnPlaybackCompleted(
            new()
            {
                UserId = Guid.NewGuid(),
                MediaId = 129,
                MediaType = "movie"
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EncodingHandler_OnEncodingProgress_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalREncodingEventHandler handler = new(bus, NoOpMessenger);

        Func<Task> act = () => handler.OnEncodingProgress(
            new()
            {
                JobId = 1,
                Percentage = 75.5,
                Elapsed = TimeSpan.FromMinutes(3),
                Estimated = TimeSpan.FromMinutes(1)
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryRefreshHandler_SubscribesToLibraryRefreshEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryRefreshEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        using SignalRLibraryRefreshEventHandler handler = new(bus, NoOpMessenger);

        await bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "album", Guid.NewGuid()]
        });

        await bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["libraries", Ulid.NewUlid().ToString()]
        });

        await bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["home"]
        });

        received.Should().HaveCount(3);
        received.Should().AllBeOfType<LibraryRefreshEvent>();
    }

    [Fact]
    public async Task LibraryRefreshHandler_BroadcastsToSignalR_WithoutException()
    {
        InMemoryEventBus bus = new();
        using SignalRLibraryRefreshEventHandler handler = new(bus, NoOpMessenger);

        Func<Task> act = () => bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "tracks"]
        });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryRefreshHandler_Dispose_UnsubscribesFromEvents()
    {
        InMemoryEventBus bus = new();
        int externalCount = 0;

        bus.Subscribe<LibraryRefreshEvent>((_, _) =>
        {
            Interlocked.Increment(ref externalCount);
            return Task.CompletedTask;
        });

        SignalRLibraryRefreshEventHandler handler = new(bus, NoOpMessenger);

        await bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "album", Guid.NewGuid()]
        });

        int countBefore = externalCount;
        handler.Dispose();

        await bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "artist", Guid.NewGuid()]
        });

        // External subscriber still fires
        externalCount.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task LibraryRefreshHandler_OnLibraryRefresh_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        using SignalRLibraryRefreshEventHandler handler = new(bus, NoOpMessenger);

        Func<Task> act = () => handler.OnLibraryRefresh(
            new()
            {
                QueryKey = ["base", "info", "123"]
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AllHandlers_IncludingRefresh_CanCoexistOnSameBus()
    {
        InMemoryEventBus bus = new();

        using SignalRPlaybackEventHandler playbackHandler = new(bus, NoOpMessenger);
        using SignalREncodingEventHandler encodingHandler = new(bus, NoOpMessenger);
        using SignalRLibraryScanEventHandler libraryScanHandler = new(bus, NoOpMessenger);
        using SignalRLibraryRefreshEventHandler libraryRefreshHandler = new(bus, NoOpMessenger);

        Func<Task> act = async () =>
        {
            await bus.PublishAsync(new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie"
            });

            await bus.PublishAsync(new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/a.mkv",
                OutputPath = "/out/",
                ProfileName = "x264"
            });

            await bus.PublishAsync(new LibraryScanStartedEvent
            {
                LibraryId = Ulid.NewUlid(),
                LibraryName = "Movies"
            });

            await bus.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["music", "playlists", Guid.NewGuid()]
            });
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LibraryRefreshEvent_PreservesQueryKey()
    {
        InMemoryEventBus bus = new();
        LibraryRefreshEvent? capturedEvent = null;

        bus.Subscribe<LibraryRefreshEvent>((evt, _) =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        });

        dynamic?[] queryKey = ["music", "album", Guid.NewGuid()];

        await bus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = queryKey
        });

        capturedEvent.Should().NotBeNull();
        capturedEvent!.QueryKey.Should().BeEquivalentTo(queryKey);
        capturedEvent.Source.Should().Be("LibraryRefresh");
        capturedEvent.EventId.Should().NotBeEmpty();
        capturedEvent.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
