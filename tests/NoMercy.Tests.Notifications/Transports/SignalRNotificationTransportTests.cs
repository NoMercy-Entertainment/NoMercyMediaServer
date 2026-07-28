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

using Microsoft.AspNetCore.SignalR;
using Moq;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.Notifications.Push;
using NoMercy.Notifications.Transports;
using Xunit;

namespace NoMercy.Tests.Notifications.Transports;

public class SignalRNotificationTransportTests
{
    private static Client ConnectedClientFor(Guid userId, out Mock<ISingleClientProxy> socket)
    {
        socket = new Mock<ISingleClientProxy>();
        socket
            .Setup(s =>
                s.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        return new Client { Sub = userId, Socket = socket.Object };
    }

    [Fact]
    public async Task CanReachAsync_UserWithLiveConnection_IsReachable()
    {
        Guid userId = Guid.NewGuid();
        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd("conn-1", ConnectedClientFor(userId, out _));

        SignalRNotificationTransport transport = new(connectedClients);

        bool reachable = await transport.CanReachAsync(userId, CancellationToken.None);

        Assert.True(reachable);
    }

    [Fact]
    public async Task CanReachAsync_UserWithNoConnection_IsNotReachable()
    {
        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd("conn-1", ConnectedClientFor(Guid.NewGuid(), out _));

        SignalRNotificationTransport transport = new(connectedClients);

        bool reachable = await transport.CanReachAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(reachable);
    }

    [Fact]
    public async Task DeliverAsync_ReachesOnlyTheTargetUsersConnections()
    {
        Guid targetUser = Guid.NewGuid();
        Guid otherUser = Guid.NewGuid();

        ConnectedClients connectedClients = new();
        Client targetClient = ConnectedClientFor(
            targetUser,
            out Mock<ISingleClientProxy> targetSocket
        );
        Client otherClient = ConnectedClientFor(
            otherUser,
            out Mock<ISingleClientProxy> otherSocket
        );
        connectedClients.Clients.TryAdd("conn-target", targetClient);
        connectedClients.Clients.TryAdd("conn-other", otherClient);

        SignalRNotificationTransport transport = new(connectedClients);
        UserNotification notification = new(
            targetUser,
            "encode-finished",
            new PushPayload("Done", "Idiocracy finished encoding", "/movie/1")
        );

        await transport.DeliverAsync(notification, CancellationToken.None);

        targetSocket.Verify(
            s =>
                s.SendCoreAsync(
                    "UserNotification",
                    It.Is<object?[]>(args =>
                        args.Length == 1 && Equals(args[0], notification.Payload)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        otherSocket.Verify(
            s =>
                s.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task DeliverAsync_SwallowsASendFailure()
    {
        Guid userId = Guid.NewGuid();
        Mock<ISingleClientProxy> socket = new();
        socket
            .Setup(s =>
                s.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("connection dropped"));

        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd(
            "conn-1",
            new Client { Sub = userId, Socket = socket.Object }
        );

        SignalRNotificationTransport transport = new(connectedClients);
        UserNotification notification = new(
            userId,
            "encode-finished",
            new PushPayload("Done", "body", null)
        );

        await transport.DeliverAsync(notification, CancellationToken.None);
    }
}
