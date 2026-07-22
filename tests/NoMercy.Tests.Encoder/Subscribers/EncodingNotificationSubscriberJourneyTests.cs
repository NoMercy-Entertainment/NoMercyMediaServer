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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Notifications;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.EventHandlers;

namespace NoMercy.Tests.Encoder.Subscribers;

/// <summary>
/// Journey tests for <see cref="EncodingNotificationSubscriber"/>: publish
/// real encoder lifecycle events through a real <see cref="InMemoryEventBus"/>
/// and assert that <see cref="INotificationDispatcher"/> receives the right
/// notification payloads.
///
/// Guard condition: when no webhook URLs are configured the subscriber skips
/// subscription entirely — publishing an event must NOT reach the dispatcher.
/// </summary>
[Trait(name: "Category", value: "Journey")]
public class EncodingNotificationSubscriberJourneyTests
{
    private static EncoderOptions WithWebhook(string url)
    {
        EncoderOptions opts = new();
        opts.NotificationWebhookUrls.Add(item: url);
        return opts;
    }

    private static EncoderOptions NoWebhooks() => new();

    [Fact]
    public async Task Journey_EncodingCompleted_Delivered_ToDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: WithWebhook(url: "https://hooks.example.com/encode"),
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);

        await bus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromMinutes(minutes: 2),
            }
        );

        dispatcher.Verify(
            expression: d =>
                d.NotifyCompletedAsync(
                    It.Is<EncodingCompletedNotification>(n =>
                        n.JobId == 1
                        && n.OutputPath == "/out/film"
                        && n.Duration == TimeSpan.FromMinutes(2)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "EncodingCompletedEvent through the real bus must reach the dispatcher with the full payload"
        );

        await subscriber.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task Journey_EncodingStarted_Delivered_ToDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(expression: d =>
                d.NotifyStartedAsync(
                    It.IsAny<EncodingStartedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: WithWebhook(url: "https://hooks.example.com/encode"),
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);

        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 2,
                InputPath = "/in/film.mkv",
                OutputPath = "/out/film",
                ProfileName = "1080p-hls",
            }
        );

        dispatcher.Verify(
            expression: d =>
                d.NotifyStartedAsync(
                    It.Is<EncodingStartedNotification>(n =>
                        n.JobId == 2
                        && n.InputPath == "/in/film.mkv"
                        && n.OutputPath == "/out/film"
                        && n.ProfileName == "1080p-hls"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "EncodingStartedEvent through the real bus must reach the dispatcher with the full payload"
        );

        await subscriber.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task Journey_EncodingFailed_Delivered_ToDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(expression: d =>
                d.NotifyFailedAsync(
                    It.IsAny<EncodingFailedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: WithWebhook(url: "https://hooks.example.com/encode"),
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);

        await bus.PublishAsync(
            @event: new EncodingFailedEvent
            {
                JobId = 3,
                InputPath = "/in/broken.mkv",
                ErrorMessage = "codec not found",
                ExceptionType = "InvalidOperationException",
            }
        );

        dispatcher.Verify(
            expression: d =>
                d.NotifyFailedAsync(
                    It.Is<EncodingFailedNotification>(n =>
                        n.JobId == 3
                        && n.InputPath == "/in/broken.mkv"
                        && n.ErrorMessage == "codec not found"
                        && n.ExceptionType == "InvalidOperationException"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "EncodingFailedEvent through the real bus must reach the dispatcher with the full payload"
        );

        await subscriber.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task Journey_NoWebhooksConfigured_BusPublish_NeverReachesDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: NoWebhooks(),
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);

        await bus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 4,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromMinutes(minutes: 1),
            }
        );

        dispatcher.Verify(
            expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "when no webhook URLs are configured the subscriber must not register — chain is severed at start"
        );

        await subscriber.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task Journey_Stop_SeversChain_EventAfterStopNotDelivered()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: WithWebhook(url: "https://hooks.example.com/encode"),
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);
        await subscriber.StopAsync(cancellationToken: CancellationToken.None);

        await bus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 5,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromMinutes(minutes: 1),
            }
        );

        dispatcher.Verify(
            expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "after StopAsync the subscriptions are disposed — the chain must be severed"
        );
    }

    [Fact]
    public async Task Journey_DispatcherThrows_BusDoesNotPropagate()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new HttpRequestException(message: "webhook endpoint unreachable"));

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: WithWebhook(url: "https://hooks.example.com/encode"),
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);

        Func<Task> act = () =>
            bus.PublishAsync(
                @event: new EncodingCompletedEvent
                {
                    JobId = 6,
                    OutputPath = "/out/film",
                    Duration = TimeSpan.Zero,
                }
            );

        await act.Should()
            .NotThrowAsync(because: "a dispatcher failure must not propagate out of the event bus");

        await subscriber.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task Journey_MultipleWebhookUrls_EachEventDeliveredOnce()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        int callCount = 0;
        dispatcher
            .Setup(expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(action: () => callCount++)
            .Returns(value: Task.CompletedTask);

        EncoderOptions options = new();
        options.NotificationWebhookUrls.Add(item: "https://a.example.com");
        options.NotificationWebhookUrls.Add(item: "https://b.example.com");

        EncodingNotificationSubscriber subscriber = new(
            eventBus: bus,
            dispatcher: dispatcher.Object,
            options: options,
            logger: NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(cancellationToken: CancellationToken.None);

        await bus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 7,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromSeconds(seconds: 30),
            }
        );

        dispatcher.Verify(
            expression: d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "the subscriber registers one handler regardless of URL count — dispatcher decides fan-out"
        );

        await subscriber.StopAsync(cancellationToken: CancellationToken.None);
    }
}
