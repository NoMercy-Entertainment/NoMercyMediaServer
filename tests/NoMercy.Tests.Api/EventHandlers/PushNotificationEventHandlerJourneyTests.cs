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
using NoMercy.Api.EventHandlers;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Auth;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Api.EventHandlers;

public class PushNotificationEventHandlerJourneyTests
{
    private static (
        InMemoryEventBus bus,
        Mock<IPushDispatcher> pushMock,
        PushNotificationEventHandler handler
    ) BuildChain(string? accessToken = "server-access-token")
    {
        InMemoryEventBus bus = new();
        Mock<IPushDispatcher> pushMock = new();
        pushMock
            .Setup(dispatcher =>
                dispatcher.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken(accessToken);

        NotificationSink sink = new(pushMock.Object);
        PushNotificationEventHandler handler = new(bus, authTokenStore, sink);
        return (bus, pushMock, handler);
    }

    [Fact]
    public async Task EncodingCompleted_PublishViaRealBus_ReachesDispatcher_WithEncodeFinishedChannel()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatcher> pushMock,
            PushNotificationEventHandler handler
        ) = BuildChain();
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 33,
                OutputPath = "/output/movie/Idiocracy.m3u8",
                Duration = TimeSpan.FromMinutes(90),
            }
        );

        pushMock.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    "encode-finished",
                    It.Is<PushPayload>(payload =>
                        payload.Title == "Encoding finished"
                        && payload.Body == "Idiocracy.m3u8 finished encoding"
                    ),
                    "server-access-token",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task EncodingFailed_PublishViaRealBus_ReachesDispatcher_WithEncodeFailedChannel()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatcher> pushMock,
            PushNotificationEventHandler handler
        ) = BuildChain();
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(
            new EncodingFailedEvent
            {
                JobId = 99,
                InputPath = "/input/corrupt.mkv",
                ErrorMessage = "FFmpeg exited with code 1",
            }
        );

        pushMock.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    "encode-failed",
                    It.Is<PushPayload>(payload =>
                        payload.Title == "Encoding failed"
                        && payload.Body == "FFmpeg exited with code 1"
                    ),
                    "server-access-token",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task LibraryRefreshed_PublishViaRealBus_ReachesDispatcher_WithLibraryUpdatedChannel()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatcher> pushMock,
            PushNotificationEventHandler handler
        ) = BuildChain();
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(new LibraryRefreshedEvent { QueryKey = ["movies", 1] });

        pushMock.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    "library-updated",
                    It.IsAny<PushPayload>(),
                    "server-access-token",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task NoAccessToken_SkipsPushEntirely()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatcher> pushMock,
            PushNotificationEventHandler handler
        ) = BuildChain(accessToken: null);
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/output/x.m3u8",
                Duration = TimeSpan.FromMinutes(1),
            }
        );

        pushMock.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Dispose_AfterSubscription_StopsPushDispatch()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatcher> pushMock,
            PushNotificationEventHandler handler
        ) = BuildChain();

        handler.Dispose();

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/output/x.m3u8",
                Duration = TimeSpan.FromMinutes(1),
            }
        );

        pushMock.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PushDispatcherFailure_DoesNotBreakTheExistingSignalRPath()
    {
        InMemoryEventBus bus = new();

        Mock<IPushDispatcher> pushMock = new();
        pushMock
            .Setup(dispatcher =>
                dispatcher.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("relay unreachable"));

        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken("server-access-token");
        NotificationSink sink = new(pushMock.Object);

        using PushNotificationEventHandler pushHandler = new(bus, authTokenStore, sink);

        Mock<NoMercy.Networking.Messaging.IClientMessenger> messengerMock = new(
            MockBehavior.Strict
        );
        messengerMock
            .Setup(messenger =>
                messenger.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())
            )
            .Returns(Task.CompletedTask);

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messengerMock.Object
        );

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/output/x.m3u8",
                Duration = TimeSpan.FromMinutes(1),
            }
        );

        pushMock.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    "encode-finished",
                    It.IsAny<PushPayload>(),
                    "server-access-token",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        messengerMock.Verify(
            messenger =>
                messenger.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()),
            Times.Once
        );
    }
}
