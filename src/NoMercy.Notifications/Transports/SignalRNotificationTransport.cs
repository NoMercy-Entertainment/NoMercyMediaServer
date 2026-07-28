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
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Notifications.Transports;

// Reaches over the SAME connection registry every hub already writes to
// (NoMercy.Networking.Messaging.ConnectedClients, populated by
// ConnectionHub.OnConnectedAsync/OnDisconnectedAsync). There is no second
// presence tracker here: a user is "reachable" exactly when that registry
// already holds a live connection for them, on any hub.
public sealed class SignalRNotificationTransport(ConnectedClients connectedClients)
    : INotificationTransport
{
    private const string EventName = "UserNotification";

    public string Name => "SignalR";

    public Task<bool> CanReachAsync(Guid userId, CancellationToken ct)
    {
        bool reachable = connectedClients.Clients.Values.Any(client => client.Sub == userId);
        return Task.FromResult(reachable);
    }

    public async Task DeliverAsync(UserNotification notification, CancellationToken ct)
    {
        List<Client> targets =
        [
            .. connectedClients.Clients.Values.Where(client => client.Sub == notification.UserId),
        ];

        await Task.WhenAll(targets.Select(client => SendOneAsync(client, notification, ct)));
    }

    private static async Task SendOneAsync(
        Client client,
        UserNotification notification,
        CancellationToken ct
    )
    {
        try
        {
            await client.Socket.SendAsync(EventName, notification.Payload, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.Socket(
                $"SignalR delivery of channel {notification.Channel} to user {notification.UserId} failed: {exception.Message}"
            );
        }
    }
}
