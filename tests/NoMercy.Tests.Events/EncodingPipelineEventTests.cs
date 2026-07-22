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
using NoMercy.Events.Encoding;
using Xunit;

namespace NoMercy.Tests.Events;

public class EncodingPipelineEventTests
{
    [Fact]
    public async Task EncodingPipeline_PublishesStartedProgressCompleted_InOrder()
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

        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 42,
                InputPath = "/input/video.mkv",
                OutputPath = "/output/video/",
                ProfileName = "HLS-1080p",
            }
        );

        await bus.PublishAsync(
            @event: new EncodingProgressUpdatedEvent
            {
                JobId = 42,
                Percentage = 25.0,
                Elapsed = TimeSpan.FromMinutes(minutes: 5),
                Estimated = TimeSpan.FromMinutes(minutes: 15),
            }
        );

        await bus.PublishAsync(
            @event: new EncodingProgressUpdatedEvent
            {
                JobId = 42,
                Percentage = 75.0,
                Elapsed = TimeSpan.FromMinutes(minutes: 15),
                Estimated = TimeSpan.FromMinutes(minutes: 5),
            }
        );

        await bus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 42,
                OutputPath = "/output/video/",
                Duration = TimeSpan.FromMinutes(minutes: 20),
            }
        );

        received.Should().HaveCount(expected: 4);
        received[index: 0].Should().BeOfType<EncodingStartedEvent>();
        received[index: 1].Should().BeOfType<EncodingProgressUpdatedEvent>();
        received[index: 2].Should().BeOfType<EncodingProgressUpdatedEvent>();
        received[index: 3].Should().BeOfType<EncodingCompletedEvent>();

        EncodingStartedEvent started = (EncodingStartedEvent)received[index: 0];
        started.JobId.Should().Be(expected: 42);
        started.InputPath.Should().Be(expected: "/input/video.mkv");
        started.ProfileName.Should().Be(expected: "HLS-1080p");

        EncodingProgressUpdatedEvent progress1 = (EncodingProgressUpdatedEvent)received[index: 1];
        progress1.Percentage.Should().Be(expected: 25.0);
        progress1.Estimated.Should().Be(expected: TimeSpan.FromMinutes(minutes: 15));

        EncodingProgressUpdatedEvent progress2 = (EncodingProgressUpdatedEvent)received[index: 2];
        progress2.Percentage.Should().Be(expected: 75.0);

        EncodingCompletedEvent completed = (EncodingCompletedEvent)received[index: 3];
        completed.Duration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 20));
    }

    [Fact]
    public async Task EncodingPipeline_PublishesStartedThenFailed_OnError()
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
        bus.Subscribe<EncodingFailedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 99,
                InputPath = "/input/corrupt.mkv",
                OutputPath = "/output/corrupt/",
                ProfileName = "HLS-720p",
            }
        );

        await bus.PublishAsync(
            @event: new EncodingFailedEvent
            {
                JobId = 99,
                InputPath = "/input/corrupt.mkv",
                ErrorMessage = "FFmpeg exited with code 1",
                ExceptionType = "InvalidOperationException",
            }
        );

        received.Should().HaveCount(expected: 2);
        received[index: 0].Should().BeOfType<EncodingStartedEvent>();
        received[index: 1].Should().BeOfType<EncodingFailedEvent>();

        EncodingFailedEvent failed = (EncodingFailedEvent)received[index: 1];
        failed.JobId.Should().Be(expected: 99);
        failed.ErrorMessage.Should().Be(expected: "FFmpeg exited with code 1");
        failed.ExceptionType.Should().Be(expected: "InvalidOperationException");
    }

    [Fact]
    public async Task EncodingProgressEvent_WorksWithGuidHashCodeAsJobId()
    {
        InMemoryEventBus bus = new();
        EncodingProgressUpdatedEvent? receivedEvent = null;

        bus.Subscribe<EncodingProgressUpdatedEvent>(
            handler: (evt, _) =>
            {
                receivedEvent = evt;
                return Task.CompletedTask;
            }
        );

        Guid trackId = Guid.NewGuid();
        int jobId = trackId.GetHashCode();

        await bus.PublishAsync(
            @event: new EncodingProgressUpdatedEvent
            {
                JobId = jobId,
                Percentage = 50.0,
                Elapsed = TimeSpan.FromMinutes(minutes: 3),
            }
        );

        receivedEvent.Should().NotBeNull();
        receivedEvent!.JobId.Should().Be(expected: jobId);
        receivedEvent.Percentage.Should().Be(expected: 50.0);
    }

    [Fact]
    public async Task EventBusProvider_CanPublishEncodingEvents_WhenConfigured()
    {
        InMemoryEventBus bus = new();
        EventBusProvider.Configure(eventBus: bus);

        List<IEvent> received = [];
        bus.Subscribe<EncodingStartedEvent>(
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

        EventBusProvider.IsConfigured.Should().BeTrue();

        await EventBusProvider.Current.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/test.mkv",
                OutputPath = "/out/",
                ProfileName = "Default",
            }
        );

        await EventBusProvider.Current.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/out/",
                Duration = TimeSpan.FromSeconds(seconds: 30),
            }
        );

        received.Should().HaveCount(expected: 2);
        received[index: 0].Should().BeOfType<EncodingStartedEvent>();
        received[index: 1].Should().BeOfType<EncodingCompletedEvent>();
    }

    [Fact]
    public async Task EncodingEvents_HaveUniqueEventIds()
    {
        EncodingStartedEvent started = new()
        {
            JobId = 1,
            InputPath = "/test",
            OutputPath = "/out",
            ProfileName = "p",
        };

        EncodingProgressUpdatedEvent progress = new()
        {
            JobId = 1,
            Percentage = 50.0,
            Elapsed = TimeSpan.FromMinutes(minutes: 1),
        };

        EncodingCompletedEvent completed = new()
        {
            JobId = 1,
            OutputPath = "/out",
            Duration = TimeSpan.FromMinutes(minutes: 2),
        };

        EncodingFailedEvent failed = new()
        {
            JobId = 1,
            InputPath = "/test",
            ErrorMessage = "error",
        };

        Guid[] eventIds = [started.EventId, progress.EventId, completed.EventId, failed.EventId];
        eventIds.Should().OnlyHaveUniqueItems();
        eventIds.Should().NotContain(unexpected: Guid.Empty);
    }

    [Fact]
    public void EncodingEvents_AllHaveEncoderSource()
    {
        IEvent[] events =
        [
            new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/i",
                OutputPath = "/o",
                ProfileName = "p",
            },
            new EncodingProgressUpdatedEvent
            {
                JobId = 1,
                Percentage = 0,
                Elapsed = TimeSpan.Zero,
            },
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/o",
                Duration = TimeSpan.Zero,
            },
            new EncodingFailedEvent
            {
                JobId = 1,
                InputPath = "/i",
                ErrorMessage = "e",
            },
        ];

        foreach (IEvent evt in events)
        {
            evt.Source.Should().Be(expected: "Encoder");
        }
    }
}
