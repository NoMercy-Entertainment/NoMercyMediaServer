using FluentAssertions;
using NoMercy.Events;
using NoMercy.Events.Playback;
using Xunit;

namespace NoMercy.Tests.Events;

public class PlaybackPipelineEventTests
{
    [Fact]
    public async Task PlaybackPipeline_PublishesStartedProgressCompleted_InOrder()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<PlaybackStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackProgressEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = userId,
            MediaId = 550,
            MediaType = "movie",
            DeviceId = "device-1"
        });

        await bus.PublishAsync(new PlaybackProgressEvent
        {
            UserId = userId,
            MediaId = 550,
            Position = TimeSpan.FromMinutes(30),
            Duration = TimeSpan.FromMinutes(120)
        });

        await bus.PublishAsync(new PlaybackProgressEvent
        {
            UserId = userId,
            MediaId = 550,
            Position = TimeSpan.FromMinutes(90),
            Duration = TimeSpan.FromMinutes(120)
        });

        await bus.PublishAsync(new PlaybackCompletedEvent
        {
            UserId = userId,
            MediaId = 550,
            MediaType = "movie"
        });

        received.Should().HaveCount(4);
        received[0].Should().BeOfType<PlaybackStartedEvent>();
        received[1].Should().BeOfType<PlaybackProgressEvent>();
        received[2].Should().BeOfType<PlaybackProgressEvent>();
        received[3].Should().BeOfType<PlaybackCompletedEvent>();

        PlaybackStartedEvent started = (PlaybackStartedEvent)received[0];
        started.UserId.Should().Be(userId);
        started.MediaId.Should().Be(550);
        started.MediaType.Should().Be("movie");
        started.DeviceId.Should().Be("device-1");

        PlaybackProgressEvent progress1 = (PlaybackProgressEvent)received[1];
        progress1.Position.Should().Be(TimeSpan.FromMinutes(30));
        progress1.Duration.Should().Be(TimeSpan.FromMinutes(120));

        PlaybackProgressEvent progress2 = (PlaybackProgressEvent)received[2];
        progress2.Position.Should().Be(TimeSpan.FromMinutes(90));

        PlaybackCompletedEvent completed = (PlaybackCompletedEvent)received[3];
        completed.MediaId.Should().Be(550);
        completed.MediaType.Should().Be("movie");
    }

    [Fact]
    public async Task PlaybackPipeline_MusicTrack_UsesMediaIdentifier()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<PlaybackStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackProgressEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        Guid userId = Guid.NewGuid();
        Guid trackId = Guid.NewGuid();

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = userId,
            MediaId = 0,
            MediaIdentifier = trackId.ToString(),
            MediaType = "music",
            DeviceId = "device-2"
        });

        await bus.PublishAsync(new PlaybackProgressEvent
        {
            UserId = userId,
            MediaId = 0,
            MediaIdentifier = trackId.ToString(),
            Position = TimeSpan.FromSeconds(90),
            Duration = TimeSpan.FromSeconds(180)
        });

        await bus.PublishAsync(new PlaybackCompletedEvent
        {
            UserId = userId,
            MediaId = 0,
            MediaIdentifier = trackId.ToString(),
            MediaType = "music"
        });

        received.Should().HaveCount(3);

        PlaybackStartedEvent started = (PlaybackStartedEvent)received[0];
        started.MediaId.Should().Be(0);
        started.MediaIdentifier.Should().Be(trackId.ToString());
        started.MediaType.Should().Be("music");

        PlaybackProgressEvent progress = (PlaybackProgressEvent)received[1];
        progress.MediaIdentifier.Should().Be(trackId.ToString());

        PlaybackCompletedEvent completed = (PlaybackCompletedEvent)received[2];
        completed.MediaIdentifier.Should().Be(trackId.ToString());
    }

    [Fact]
    public async Task PlaybackEvents_HaveUniqueEventIds()
    {
        Guid userId = Guid.NewGuid();

        PlaybackStartedEvent started = new()
        {
            UserId = userId,
            MediaId = 1,
            MediaType = "movie"
        };

        PlaybackProgressEvent progress = new()
        {
            UserId = userId,
            MediaId = 1,
            Position = TimeSpan.FromMinutes(10),
            Duration = TimeSpan.FromMinutes(120)
        };

        PlaybackCompletedEvent completed = new()
        {
            UserId = userId,
            MediaId = 1,
            MediaType = "movie"
        };

        Guid[] eventIds = [started.EventId, progress.EventId, completed.EventId];
        eventIds.Should().OnlyHaveUniqueItems();
        eventIds.Should().NotContain(Guid.Empty);
    }

    [Fact]
    public void PlaybackEvents_AllHavePlaybackSource()
    {
        Guid userId = Guid.NewGuid();

        IEvent[] events =
        [
            new PlaybackStartedEvent { UserId = userId, MediaId = 1, MediaType = "movie" },
            new PlaybackProgressEvent { UserId = userId, MediaId = 1, Position = TimeSpan.Zero, Duration = TimeSpan.Zero },
            new PlaybackCompletedEvent { UserId = userId, MediaId = 1, MediaType = "movie" }
        ];

        foreach (IEvent evt in events)
        {
            evt.Source.Should().Be("Playback");
        }
    }

    [Fact]
    public async Task PlaybackStartedEvent_MediaIdentifier_IsOptional()
    {
        InMemoryEventBus bus = new();
        PlaybackStartedEvent? receivedEvent = null;

        bus.Subscribe<PlaybackStartedEvent>((evt, _) =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = Guid.NewGuid(),
            MediaId = 550,
            MediaType = "movie"
        });

        receivedEvent.Should().NotBeNull();
        receivedEvent!.MediaIdentifier.Should().BeNull();
        receivedEvent.MediaId.Should().Be(550);
    }

    [Fact]
    public async Task EventBusProvider_CanPublishPlaybackEvents_WhenConfigured()
    {
        InMemoryEventBus bus = new();
        EventBusProvider.Configure(bus);

        List<IEvent> received = [];
        bus.Subscribe<PlaybackStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<PlaybackCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        EventBusProvider.IsConfigured.Should().BeTrue();

        Guid userId = Guid.NewGuid();

        await EventBusProvider.Current.PublishAsync(new PlaybackStartedEvent
        {
            UserId = userId,
            MediaId = 42,
            MediaType = "tv",
            DeviceId = "test-device"
        });

        await EventBusProvider.Current.PublishAsync(new PlaybackCompletedEvent
        {
            UserId = userId,
            MediaId = 42,
            MediaType = "tv"
        });

        received.Should().HaveCount(2);
        received[0].Should().BeOfType<PlaybackStartedEvent>();
        received[1].Should().BeOfType<PlaybackCompletedEvent>();
    }

    [Fact]
    public void PlaybackEvents_HaveTimestampsSetAutomatically()
    {
        DateTime before = DateTime.UtcNow;

        PlaybackStartedEvent started = new()
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "movie"
        };

        DateTime after = DateTime.UtcNow;

        started.Timestamp.Should().BeOnOrAfter(before);
        started.Timestamp.Should().BeOnOrBefore(after);
    }
}
