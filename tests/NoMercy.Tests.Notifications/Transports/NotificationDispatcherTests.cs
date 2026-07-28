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

using Moq;
using NoMercy.Notifications.Push;
using NoMercy.Notifications.Transports;
using Xunit;

namespace NoMercy.Tests.Notifications.Transports;

public class NotificationDispatcherTests
{
    private static UserNotification ANotification(Guid? userId = null) =>
        new(
            userId ?? Guid.NewGuid(),
            "videoHub",
            "encode-finished",
            new PushPayload("Done", "body", null)
        );

    private static Mock<INotificationTransport> FakeTransport(
        string name,
        bool reachable,
        Func<Exception>? canReachThrows = null,
        Func<Exception>? deliverThrows = null
    )
    {
        Mock<INotificationTransport> transport = new();
        transport.SetupGet(t => t.Name).Returns(name);

        if (canReachThrows is not null)
        {
            transport
                .Setup(t =>
                    t.CanReachAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>())
                )
                .ThrowsAsync(canReachThrows());
        }
        else
        {
            transport
                .Setup(t =>
                    t.CanReachAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(reachable);
        }

        if (deliverThrows is not null)
        {
            transport
                .Setup(t =>
                    t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>())
                )
                .ThrowsAsync(deliverThrows());
        }
        else
        {
            transport
                .Setup(t =>
                    t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>())
                )
                .Returns(Task.CompletedTask);
        }

        return transport;
    }

    [Fact]
    public async Task AConnectedUser_GetsSignalR_AndNoPush()
    {
        Mock<INotificationTransport> signalR = FakeTransport("SignalR", reachable: true);
        Mock<INotificationTransport> push = FakeTransport("Push", reachable: true);

        NotificationDispatcher dispatcher = new([signalR.Object, push.Object]);

        await dispatcher.DispatchAsync(ANotification(), CancellationToken.None);

        signalR.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        push.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ADisconnectedUser_GetsPush_AndNoSignalR()
    {
        Mock<INotificationTransport> signalR = FakeTransport("SignalR", reachable: false);
        Mock<INotificationTransport> push = FakeTransport("Push", reachable: true);

        NotificationDispatcher dispatcher = new([signalR.Object, push.Object]);

        await dispatcher.DispatchAsync(ANotification(), CancellationToken.None);

        signalR.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        push.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task AUserReachableByNeither_IsASilentNoOp()
    {
        Mock<INotificationTransport> signalR = FakeTransport("SignalR", reachable: false);
        Mock<INotificationTransport> push = FakeTransport("Push", reachable: false);

        NotificationDispatcher dispatcher = new([signalR.Object, push.Object]);

        Exception? exception = await Record.ExceptionAsync(() =>
            dispatcher.DispatchAsync(ANotification(), CancellationToken.None)
        );

        Assert.Null(exception);
        signalR.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        push.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ATransportThatThrowsFromCanReach_DoesNotStopTheNextBeingTried()
    {
        Mock<INotificationTransport> signalR = FakeTransport(
            "SignalR",
            reachable: false,
            canReachThrows: () => new InvalidOperationException("hub registry unavailable")
        );
        Mock<INotificationTransport> push = FakeTransport("Push", reachable: true);

        NotificationDispatcher dispatcher = new([signalR.Object, push.Object]);

        Exception? exception = await Record.ExceptionAsync(() =>
            dispatcher.DispatchAsync(ANotification(), CancellationToken.None)
        );

        Assert.Null(exception);
        push.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ATransportThatThrowsFromDeliver_DoesNotStopTheNextBeingTried()
    {
        Mock<INotificationTransport> signalR = FakeTransport(
            "SignalR",
            reachable: true,
            deliverThrows: () => new InvalidOperationException("socket write failed")
        );
        Mock<INotificationTransport> push = FakeTransport("Push", reachable: true);

        NotificationDispatcher dispatcher = new([signalR.Object, push.Object]);

        Exception? exception = await Record.ExceptionAsync(() =>
            dispatcher.DispatchAsync(ANotification(), CancellationToken.None)
        );

        Assert.Null(exception);
        push.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task OrderIsDeterministic_EvenWhenTransportsAreRegisteredInReverse()
    {
        Mock<INotificationTransport> signalR = FakeTransport("SignalR", reachable: true);
        Mock<INotificationTransport> push = FakeTransport("Push", reachable: true);

        NotificationDispatcher dispatcher = new([push.Object, signalR.Object]);

        await dispatcher.DispatchAsync(ANotification(), CancellationToken.None);

        signalR.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        push.Verify(
            t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
