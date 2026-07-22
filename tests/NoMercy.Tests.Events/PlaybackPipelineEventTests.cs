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

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 129,
                MediaType = "movie",
                DeviceId = "device-1",
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 129,
                Position = TimeSpan.FromMinutes(minutes: 30),
                Duration = TimeSpan.FromMinutes(minutes: 120),
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 129,
                Position = TimeSpan.FromMinutes(minutes: 90),
                Duration = TimeSpan.FromMinutes(minutes: 120),
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 129,
                MediaType = "movie",
            }
        );

        received.Should().HaveCount(expected: 4);
        received[index: 0].Should().BeOfType<PlaybackStartedEvent>();
        received[index: 1].Should().BeOfType<PlaybackProgressUpdatedEvent>();
        received[index: 2].Should().BeOfType<PlaybackProgressUpdatedEvent>();
        received[index: 3].Should().BeOfType<PlaybackCompletedEvent>();

        PlaybackStartedEvent started = (PlaybackStartedEvent)received[index: 0];
        started.UserId.Should().Be(expected: userId);
        started.MediaId.Should().Be(expected: 129);
        started.MediaType.Should().Be(expected: "movie");
        started.DeviceId.Should().Be(expected: "device-1");

        PlaybackProgressUpdatedEvent progress1 = (PlaybackProgressUpdatedEvent)received[index: 1];
        progress1.Position.Should().Be(expected: TimeSpan.FromMinutes(minutes: 30));
        progress1.Duration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 120));

        PlaybackProgressUpdatedEvent progress2 = (PlaybackProgressUpdatedEvent)received[index: 2];
        progress2.Position.Should().Be(expected: TimeSpan.FromMinutes(minutes: 90));

        PlaybackCompletedEvent completed = (PlaybackCompletedEvent)received[index: 3];
        completed.MediaId.Should().Be(expected: 129);
        completed.MediaType.Should().Be(expected: "movie");
    }

    [Fact]
    public async Task PlaybackPipeline_MusicTrack_UsesMediaIdentifier()
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

        Guid userId = Guid.NewGuid();
        Guid trackId = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = trackId.ToString(),
                MediaType = "music",
                DeviceId = "device-2",
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = trackId.ToString(),
                Position = TimeSpan.FromSeconds(seconds: 90),
                Duration = TimeSpan.FromSeconds(seconds: 180),
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = trackId.ToString(),
                MediaType = "music",
            }
        );

        received.Should().HaveCount(expected: 3);

        PlaybackStartedEvent started = (PlaybackStartedEvent)received[index: 0];
        started.MediaId.Should().Be(expected: 0);
        started.MediaIdentifier.Should().Be(expected: trackId.ToString());
        started.MediaType.Should().Be(expected: "music");

        PlaybackProgressUpdatedEvent progress = (PlaybackProgressUpdatedEvent)received[index: 1];
        progress.MediaIdentifier.Should().Be(expected: trackId.ToString());

        PlaybackCompletedEvent completed = (PlaybackCompletedEvent)received[index: 2];
        completed.MediaIdentifier.Should().Be(expected: trackId.ToString());
    }

    [Fact]
    public async Task PlaybackEvents_HaveUniqueEventIds()
    {
        Guid userId = Guid.NewGuid();

        PlaybackStartedEvent started = new()
        {
            UserId = userId,
            MediaId = 1,
            MediaType = "movie",
        };

        PlaybackProgressUpdatedEvent progress = new()
        {
            UserId = userId,
            MediaId = 1,
            Position = TimeSpan.FromMinutes(minutes: 10),
            Duration = TimeSpan.FromMinutes(minutes: 120),
        };

        PlaybackCompletedEvent completed = new()
        {
            UserId = userId,
            MediaId = 1,
            MediaType = "movie",
        };

        Guid[] eventIds = [started.EventId, progress.EventId, completed.EventId];
        eventIds.Should().OnlyHaveUniqueItems();
        eventIds.Should().NotContain(unexpected: Guid.Empty);
    }

    [Fact]
    public void PlaybackEvents_AllHavePlaybackSource()
    {
        Guid userId = Guid.NewGuid();

        IEvent[] events =
        [
            new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 1,
                MediaType = "movie",
            },
            new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 1,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero,
            },
            new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 1,
                MediaType = "movie",
            },
        ];

        foreach (IEvent evt in events)
        {
            evt.Source.Should().Be(expected: "Playback");
        }
    }

    [Fact]
    public async Task PlaybackStartedEvent_MediaIdentifier_IsOptional()
    {
        InMemoryEventBus bus = new();
        PlaybackStartedEvent? receivedEvent = null;

        bus.Subscribe<PlaybackStartedEvent>(
            handler: (evt, _) =>
            {
                receivedEvent = evt;
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 129,
                MediaType = "movie",
            }
        );

        receivedEvent.Should().NotBeNull();
        receivedEvent!.MediaIdentifier.Should().BeNull();
        receivedEvent.MediaId.Should().Be(expected: 129);
    }

    [Fact]
    public async Task EventBusProvider_CanPublishPlaybackEvents_WhenConfigured()
    {
        InMemoryEventBus bus = new();
        EventBusProvider.Configure(eventBus: bus);

        List<IEvent> received = [];
        bus.Subscribe<PlaybackStartedEvent>(
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

        EventBusProvider.IsConfigured.Should().BeTrue();

        Guid userId = Guid.NewGuid();

        await EventBusProvider.Current.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 42,
                MediaType = "tv",
                DeviceId = "test-device",
            }
        );

        await EventBusProvider.Current.PublishAsync(
            @event: new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 42,
                MediaType = "tv",
            }
        );

        received.Should().HaveCount(expected: 2);
        received[index: 0].Should().BeOfType<PlaybackStartedEvent>();
        received[index: 1].Should().BeOfType<PlaybackCompletedEvent>();
    }

    [Fact]
    public void PlaybackEvents_HaveTimestampsSetAutomatically()
    {
        DateTime before = DateTime.UtcNow;

        PlaybackStartedEvent started = new()
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "movie",
        };

        DateTime after = DateTime.UtcNow;

        started.Timestamp.Should().BeOnOrAfter(expected: before);
        started.Timestamp.Should().BeOnOrBefore(expected: after);
    }
}
