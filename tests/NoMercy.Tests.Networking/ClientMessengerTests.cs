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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// Covers the live-incident fix to <see cref="ClientMessenger"/>: broadcasts used to await
/// each target connection sequentially, so one slow or half-dead connection (a backgrounded
/// phone, a zombie socket) delayed — and, depending on dictionary enumeration order, could
/// starve — delivery to every other connection in the same broadcast. Dispatch must now be
/// concurrent per connection, and a failing/slow connection must never prevent (or measurably
/// delay) delivery to a healthy one.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ClientMessengerTests
{
    private static Client MakeClient(
        Guid userId,
        string endpoint,
        Mock<ISingleClientProxy> proxy,
        string deviceId = "device-a"
    )
    {
        return new()
        {
            Id = Ulid.NewUlid(),
            Sub = userId,
            DeviceId = deviceId,
            Endpoint = endpoint,
            Socket = proxy.Object,
        };
    }

    [Fact]
    public async Task SendTo_DispatchesConcurrently_SlowConnectionDoesNotDelayFastOne()
    {
        ConnectedClients connectedClients = new();
        Guid userId = Guid.NewGuid();

        Mock<ISingleClientProxy> slowProxy = new();
        slowProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: async () => await Task.Delay(millisecondsDelay: 500));

        Mock<ISingleClientProxy> fastProxy = new();
        TaskCompletionSource fastProxyCalled = new();
        fastProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(action: () => fastProxyCalled.TrySetResult())
            .Returns(value: Task.CompletedTask);

        connectedClients.Clients[key: "slow-connection"] = MakeClient(
            userId: userId,
            endpoint: "/musicHub",
            proxy: slowProxy,
            deviceId: "device-slow"
        );
        connectedClients.Clients[key: "fast-connection"] = MakeClient(
            userId: userId,
            endpoint: "/musicHub",
            proxy: fastProxy,
            deviceId: "device-fast"
        );

        ClientMessenger messenger = new(connectedClients: connectedClients, logger: NullLogger<ClientMessenger>.Instance);

        Task sendTask = messenger.SendTo(name: "MusicPlayerState", endpoint: "musicHub", userId: userId, data: new { });

        Task completed = await Task.WhenAny(
            task1: fastProxyCalled.Task,
            task2: Task.Delay(delay: TimeSpan.FromMilliseconds(milliseconds: 250))
        );

        // The fast connection's send must be observed well inside the slow
        // connection's 500ms delay, proving the two dispatches run concurrently
        // rather than the fast one queuing behind the slow one.
        completed.Should().BeSameAs(expected: fastProxyCalled.Task);

        await sendTask;
    }

    [Fact]
    public async Task SendTo_OneConnectionThrows_OtherConnectionStillReceivesMessage()
    {
        ConnectedClients connectedClients = new();
        Guid userId = Guid.NewGuid();

        Mock<ISingleClientProxy> throwingProxy = new();
        throwingProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "connection is gone"));

        Mock<ISingleClientProxy> healthyProxy = new();
        healthyProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        connectedClients.Clients[key: "dead-connection"] = MakeClient(
            userId: userId,
            endpoint: "/musicHub",
            proxy: throwingProxy,
            deviceId: "device-dead"
        );
        connectedClients.Clients[key: "healthy-connection"] = MakeClient(
            userId: userId,
            endpoint: "/musicHub",
            proxy: healthyProxy,
            deviceId: "device-healthy"
        );

        ClientMessenger messenger = new(connectedClients: connectedClients, logger: NullLogger<ClientMessenger>.Instance);

        Func<Task> act = () => messenger.SendTo(name: "MusicPlayerState", endpoint: "musicHub", userId: userId, data: new { });

        await act.Should().NotThrowAsync();

        healthyProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "MusicPlayerState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task SendToAll_OneConnectionThrows_OtherConnectionStillReceivesMessage()
    {
        ConnectedClients connectedClients = new();

        Mock<ISingleClientProxy> throwingProxy = new();
        throwingProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "connection is gone"));

        Mock<ISingleClientProxy> healthyProxy = new();
        healthyProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        connectedClients.Clients[key: "dead-connection"] = MakeClient(
            userId: Guid.NewGuid(),
            endpoint: "/musicHub",
            proxy: throwingProxy,
            deviceId: "device-dead"
        );
        connectedClients.Clients[key: "healthy-connection"] = MakeClient(
            userId: Guid.NewGuid(),
            endpoint: "/musicHub",
            proxy: healthyProxy,
            deviceId: "device-healthy"
        );

        ClientMessenger messenger = new(connectedClients: connectedClients, logger: NullLogger<ClientMessenger>.Instance);

        Func<Task> act = () => messenger.SendToAll(name: "ConnectedDevicesState", endpoint: "musicHub", data: new { });

        await act.Should().NotThrowAsync();

        healthyProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "ConnectedDevicesState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task SendTo_OnlyTargetsMatchingUserAndEndpoint()
    {
        ConnectedClients connectedClients = new();
        Guid targetUser = Guid.NewGuid();
        Guid otherUser = Guid.NewGuid();

        Mock<ISingleClientProxy> targetProxy = new();
        targetProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        Mock<ISingleClientProxy> otherUserProxy = new();
        Mock<ISingleClientProxy> otherEndpointProxy = new();

        connectedClients.Clients[key: "target"] = MakeClient(userId: targetUser, endpoint: "/musicHub", proxy: targetProxy);
        connectedClients.Clients[key: "other-user"] = MakeClient(userId: otherUser, endpoint: "/musicHub", proxy: otherUserProxy);
        connectedClients.Clients[key: "other-endpoint"] = MakeClient(
            userId: targetUser,
            endpoint: "/videoHub",
            proxy: otherEndpointProxy
        );

        ClientMessenger messenger = new(connectedClients: connectedClients, logger: NullLogger<ClientMessenger>.Instance);

        await messenger.SendTo(name: "MusicPlayerState", endpoint: "musicHub", userId: targetUser, data: new { });

        targetProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "MusicPlayerState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
        otherUserProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
        otherEndpointProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }
}
