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
[Trait("Category", "Journey")]
public class EncodingNotificationSubscriberJourneyTests
{
    private static EncoderOptions WithWebhook(string url)
    {
        EncoderOptions opts = new();
        opts.NotificationWebhookUrls.Add(url);
        return opts;
    }

    private static EncoderOptions NoWebhooks() => new();

    [Fact]
    public async Task Journey_EncodingCompleted_Delivered_ToDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            WithWebhook("https://hooks.example.com/encode"),
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromMinutes(2),
            }
        );

        dispatcher.Verify(
            d =>
                d.NotifyCompletedAsync(
                    It.Is<EncodingCompletedNotification>(n =>
                        n.JobId == 1
                        && n.OutputPath == "/out/film"
                        && n.Duration == TimeSpan.FromMinutes(2)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "EncodingCompletedEvent through the real bus must reach the dispatcher with the full payload"
        );

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Journey_EncodingStarted_Delivered_ToDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.NotifyStartedAsync(
                    It.IsAny<EncodingStartedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            WithWebhook("https://hooks.example.com/encode"),
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(
            new EncodingStartedEvent
            {
                JobId = 2,
                InputPath = "/in/film.mkv",
                OutputPath = "/out/film",
                ProfileName = "1080p-hls",
            }
        );

        dispatcher.Verify(
            d =>
                d.NotifyStartedAsync(
                    It.Is<EncodingStartedNotification>(n =>
                        n.JobId == 2
                        && n.InputPath == "/in/film.mkv"
                        && n.OutputPath == "/out/film"
                        && n.ProfileName == "1080p-hls"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "EncodingStartedEvent through the real bus must reach the dispatcher with the full payload"
        );

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Journey_EncodingFailed_Delivered_ToDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.NotifyFailedAsync(
                    It.IsAny<EncodingFailedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            WithWebhook("https://hooks.example.com/encode"),
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(
            new EncodingFailedEvent
            {
                JobId = 3,
                InputPath = "/in/broken.mkv",
                ErrorMessage = "codec not found",
                ExceptionType = "InvalidOperationException",
            }
        );

        dispatcher.Verify(
            d =>
                d.NotifyFailedAsync(
                    It.Is<EncodingFailedNotification>(n =>
                        n.JobId == 3
                        && n.InputPath == "/in/broken.mkv"
                        && n.ErrorMessage == "codec not found"
                        && n.ExceptionType == "InvalidOperationException"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "EncodingFailedEvent through the real bus must reach the dispatcher with the full payload"
        );

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Journey_NoWebhooksConfigured_BusPublish_NeverReachesDispatcher()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            NoWebhooks(),
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 4,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromMinutes(1),
            }
        );

        dispatcher.Verify(
            d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "when no webhook URLs are configured the subscriber must not register — chain is severed at start"
        );

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Journey_Stop_SeversChain_EventAfterStopNotDelivered()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            WithWebhook("https://hooks.example.com/encode"),
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);
        await subscriber.StopAsync(CancellationToken.None);

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 5,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromMinutes(1),
            }
        );

        dispatcher.Verify(
            d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "after StopAsync the subscriptions are disposed — the chain must be severed"
        );
    }

    [Fact]
    public async Task Journey_DispatcherThrows_BusDoesNotPropagate()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("webhook endpoint unreachable"));

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            WithWebhook("https://hooks.example.com/encode"),
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);

        Func<Task> act = () =>
            bus.PublishAsync(
                new EncodingCompletedEvent
                {
                    JobId = 6,
                    OutputPath = "/out/film",
                    Duration = TimeSpan.Zero,
                }
            );

        await act.Should()
            .NotThrowAsync("a dispatcher failure must not propagate out of the event bus");

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Journey_MultipleWebhookUrls_EachEventDeliveredOnce()
    {
        InMemoryEventBus bus = new();
        Mock<INotificationDispatcher> dispatcher = new();
        int callCount = 0;
        dispatcher
            .Setup(d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => callCount++)
            .Returns(Task.CompletedTask);

        EncoderOptions options = new();
        options.NotificationWebhookUrls.Add("https://a.example.com");
        options.NotificationWebhookUrls.Add("https://b.example.com");

        EncodingNotificationSubscriber subscriber = new(
            bus,
            dispatcher.Object,
            options,
            NullLogger<EncodingNotificationSubscriber>.Instance
        );
        await subscriber.StartAsync(CancellationToken.None);

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 7,
                OutputPath = "/out/film",
                Duration = TimeSpan.FromSeconds(30),
            }
        );

        dispatcher.Verify(
            d =>
                d.NotifyCompletedAsync(
                    It.IsAny<EncodingCompletedNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "the subscriber registers one handler regardless of URL count — dispatcher decides fan-out"
        );

        await subscriber.StopAsync(CancellationToken.None);
    }
}
