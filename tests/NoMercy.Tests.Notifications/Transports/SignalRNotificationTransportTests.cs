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
using NoMercy.Networking.Dto;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.Notifications.Push;
using NoMercy.Notifications.Transports;
using Xunit;

namespace NoMercy.Tests.Notifications.Transports;

public class SignalRNotificationTransportTests
{
    private static Client ConnectedClientFor(
        Guid userId,
        out Mock<ISingleClientProxy> socket,
        string hub = "videoHub"
    )
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

        return new Client
        {
            Sub = userId,
            Socket = socket.Object,
            Endpoint = "/" + hub,
        };
    }

    private static UserNotification ANotification(
        Guid userId,
        string hub = "videoHub",
        string? route = "/movie/1",
        string? category = "info"
    ) =>
        new(
            userId,
            hub,
            "user-notification",
            new PushPayload("Done", "Idiocracy finished encoding", route, category)
        );

    [Fact]
    public async Task CanReachAsync_UserWithLiveConnectionOnTheTargetHub_IsReachable()
    {
        Guid userId = Guid.NewGuid();
        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd("conn-1", ConnectedClientFor(userId, out _));

        SignalRNotificationTransport transport = new(connectedClients);

        bool reachable = await transport.CanReachAsync(
            ANotification(userId),
            CancellationToken.None
        );

        Assert.True(reachable);
    }

    [Fact]
    public async Task CanReachAsync_UserWithNoConnection_IsNotReachable()
    {
        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd("conn-1", ConnectedClientFor(Guid.NewGuid(), out _));

        SignalRNotificationTransport transport = new(connectedClients);

        bool reachable = await transport.CanReachAsync(
            ANotification(Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.False(reachable);
    }

    /// <summary>
    /// Delivery only ever reaches the connections a user holds on the target
    /// hub. Reporting a user connected to musicHub as reachable on videoHub
    /// suppresses their push and then sends to nothing, which is the one
    /// outcome — neither transport — the dispatcher exists to rule out.
    /// </summary>
    [Fact]
    public async Task CanReachAsync_UserConnectedToADifferentHubOnly_IsNotReachable()
    {
        Guid userId = Guid.NewGuid();
        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd(
            "conn-music",
            ConnectedClientFor(userId, out _, hub: "musicHub")
        );

        SignalRNotificationTransport transport = new(connectedClients);

        bool reachable = await transport.CanReachAsync(
            ANotification(userId, hub: "videoHub"),
            CancellationToken.None
        );

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

        await transport.DeliverAsync(ANotification(targetUser), CancellationToken.None);

        targetSocket.Verify(
            s =>
                s.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
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
    public async Task DeliverAsync_DoesNotReachTheUsersConnectionsOnOtherHubs()
    {
        Guid userId = Guid.NewGuid();

        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd(
            "conn-video",
            ConnectedClientFor(userId, out Mock<ISingleClientProxy> videoSocket)
        );
        connectedClients.Clients.TryAdd(
            "conn-music",
            ConnectedClientFor(userId, out Mock<ISingleClientProxy> musicSocket, hub: "musicHub")
        );

        SignalRNotificationTransport transport = new(connectedClients);

        await transport.DeliverAsync(
            ANotification(userId, hub: "videoHub"),
            CancellationToken.None
        );

        videoSocket.Verify(
            s =>
                s.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        musicSocket.Verify(
            s =>
                s.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    /// <summary>
    /// "Notify" carrying a NotifyDto is the shipped contract nomercy-app-web's
    /// socketClient renders as a toast. This transport and
    /// SignalRNotificationEventHandler both feed it, and a client must not be
    /// able to tell which one produced the message it received.
    /// </summary>
    [Fact]
    public async Task DeliverAsync_EmitsTheShippedNotifyEvent_WithTheShippedPayloadShape()
    {
        Guid userId = Guid.NewGuid();

        ConnectedClients connectedClients = new();
        connectedClients.Clients.TryAdd(
            "conn-1",
            ConnectedClientFor(userId, out Mock<ISingleClientProxy> socket)
        );

        SignalRNotificationTransport transport = new(connectedClients);

        object?[]? captured = null;
        socket
            .Setup(s =>
                s.SendCoreAsync("Notify", It.IsAny<object?[]>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, object?[], CancellationToken>((_, args, _) => captured = args)
            .Returns(Task.CompletedTask);

        await transport.DeliverAsync(
            ANotification(userId, route: "/movie/1", category: "info"),
            CancellationToken.None
        );

        Assert.NotNull(captured);
        NotifyDto payload = Assert.IsType<NotifyDto>(Assert.Single(captured!));
        Assert.Equal("Done", payload.Title);
        Assert.Equal("Idiocracy finished encoding", payload.Message);
        Assert.Equal("info", payload.Type);
        Assert.Equal("/movie/1", payload.Route);
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
            new Client
            {
                Sub = userId,
                Socket = socket.Object,
                Endpoint = "/videoHub",
            }
        );

        SignalRNotificationTransport transport = new(connectedClients);

        await transport.DeliverAsync(ANotification(userId), CancellationToken.None);
    }
}
