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
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static (
        InMemoryEventBus bus,
        Mock<IPushDispatchQueue> queueMock,
        PushNotificationEventHandler handler
    ) BuildChain(string? accessToken = "server-access-token")
    {
        InMemoryEventBus bus = new();
        Mock<IPushDispatchQueue> queueMock = new();

        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken(accessToken);

        NotificationSink sink = new(queueMock.Object);
        PushNotificationEventHandler handler = new(bus, authTokenStore, sink);
        return (bus, queueMock, handler);
    }

    private static EncodingCompletedEvent AnEncodeFinishing(
        string outputPath = "/output/movie/Idiocracy.m3u8"
    ) =>
        new()
        {
            JobId = 33,
            OutputPath = outputPath,
            Duration = TimeSpan.FromMinutes(90),
        };

    [Fact]
    public async Task EncodingCompleted_PublishViaRealBus_ReachesTheQueue_WithEncodeFinishedChannel()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatchQueue> queueMock,
            PushNotificationEventHandler handler
        ) = BuildChain();
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(AnEncodeFinishing());

        queueMock.Verify(
            queue =>
                queue.Enqueue(
                    It.Is<PushDispatchRequest>(request =>
                        request.Channel == "encode-finished"
                        && request.Payload.Title == "Encoding finished"
                        && request.Payload.Body == "Idiocracy.m3u8 finished encoding"
                        && request.AccessToken == "server-access-token"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task EncodingFailed_PublishViaRealBus_ReachesTheQueue_WithEncodeFailedChannel()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatchQueue> queueMock,
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

        queueMock.Verify(
            queue =>
                queue.Enqueue(
                    It.Is<PushDispatchRequest>(request =>
                        request.Channel == "encode-failed"
                        && request.Payload.Title == "Encoding failed"
                        && request.Payload.Body == "FFmpeg exited with code 1"
                    )
                ),
            Times.Once
        );
    }

    /// <summary>
    /// LibraryRefreshedEvent is a cache-invalidation signal carrying a
    /// QueryKey. It is published from dozens of call sites and fires several
    /// times for one user action, including every continue-watching edit, so
    /// wiring it to push means every member's devices buzz on every scrub.
    /// </summary>
    [Fact]
    public async Task LibraryRefreshed_IsACacheSignal_AndPushesNothing()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatchQueue> queueMock,
            PushNotificationEventHandler handler
        ) = BuildChain();
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(new LibraryRefreshedEvent { QueryKey = ["movies", 1] });
        await bus.PublishAsync(new LibraryRefreshedEvent { QueryKey = ["continue", 1] });

        queueMock.Verify(queue => queue.Enqueue(It.IsAny<PushDispatchRequest>()), Times.Never);
    }

    [Fact]
    public async Task NoAccessToken_SkipsPushEntirely()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatchQueue> queueMock,
            PushNotificationEventHandler handler
        ) = BuildChain(accessToken: null);
        using PushNotificationEventHandler _ = handler;

        await bus.PublishAsync(AnEncodeFinishing());

        queueMock.Verify(queue => queue.Enqueue(It.IsAny<PushDispatchRequest>()), Times.Never);
    }

    [Fact]
    public async Task Dispose_AfterSubscription_StopsPushDispatch()
    {
        (
            InMemoryEventBus bus,
            Mock<IPushDispatchQueue> queueMock,
            PushNotificationEventHandler handler
        ) = BuildChain();

        handler.Dispose();

        await bus.PublishAsync(AnEncodeFinishing());

        queueMock.Verify(queue => queue.Enqueue(It.IsAny<PushDispatchRequest>()), Times.Never);
    }

    /// <summary>
    /// InMemoryEventBus awaits its subscribers one after another. With the
    /// relay call inline, an unreachable nomercy.tv adds its full HTTP timeout
    /// to every publish, and a request that publishes four events waits four
    /// times over — on a self-hosted server that has to stay fully usable
    /// whether or not it can reach NoMercy.
    /// </summary>
    [Fact]
    public async Task PublishDoesNotWaitForTheRelay_EvenWhenItNeverAnswers()
    {
        TaskCompletionSource entered = new();
        TaskCompletionSource relayNeverAnswers = new();

        Mock<IPushDispatcher> dispatcherMock = new();
        dispatcherMock
            .Setup(dispatcher =>
                dispatcher.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                entered.TrySetResult();
                return relayNeverAnswers.Task;
            });

        InMemoryEventBus bus = new();
        PushDispatchQueue queue = new(dispatcherMock.Object);
        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken("server-access-token");

        using CancellationTokenSource cts = new();
        Task drain = queue.DrainAsync(cts.Token);

        using PushNotificationEventHandler _ = new(bus, authTokenStore, new(queue));

        await bus.PublishAsync(AnEncodeFinishing());
        await entered.Task.WaitAsync(Patience);

        Task laterPublishes = Task.WhenAll(
            bus.PublishAsync(AnEncodeFinishing("/output/movie/Gattaca.m3u8")),
            bus.PublishAsync(AnEncodeFinishing("/output/movie/Primer.m3u8")),
            bus.PublishAsync(AnEncodeFinishing("/output/movie/Coherence.m3u8"))
        );

        Assert.True(laterPublishes.IsCompletedSuccessfully);
        Assert.False(relayNeverAnswers.Task.IsCompleted);

        relayNeverAnswers.SetResult();
        await cts.CancelAsync();
    }

    [Fact]
    public async Task PushDispatcherFailure_DoesNotBreakTheExistingSignalRPath()
    {
        InMemoryEventBus bus = new();

        Mock<IPushDispatcher> dispatcherMock = new();
        dispatcherMock
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

        PushDispatchQueue queue = new(dispatcherMock.Object);
        using CancellationTokenSource cts = new();
        Task drain = queue.DrainAsync(cts.Token);

        using PushNotificationEventHandler pushHandler = new(bus, authTokenStore, new(queue));

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

        await bus.PublishAsync(AnEncodeFinishing("/output/x.m3u8"));

        messengerMock.Verify(
            messenger =>
                messenger.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()),
            Times.Once
        );

        await cts.CancelAsync();
    }
}
