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
using NoMercy.Networking.Dto;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Notifications.Transports;

public sealed class SignalRNotificationTransport(ConnectedClients connectedClients)
    : INotificationTransport
{
    // The already-shipped wire contract: nomercy-app-web's socketClient listens
    // for "notify" and renders a toast from a NotifyDto. A client must not be
    // able to tell whether this transport or SignalRNotificationEventHandler
    // produced the message.
    private const string EventName = "Notify";

    public string Name => "SignalR";

    public Task<bool> CanReachAsync(UserNotification notification, CancellationToken ct)
    {
        return Task.FromResult(connectedClients.IsReachable(notification.UserId, notification.Hub));
    }

    public async Task DeliverAsync(UserNotification notification, CancellationToken ct)
    {
        List<KeyValuePair<string, Client>> targets = connectedClients.ConnectionsFor(
            notification.UserId,
            notification.Hub
        );

        NotifyDto payload = new()
        {
            Title = notification.Payload.Title,
            Message = notification.Payload.Body,
            Type = notification.Payload.Category ?? string.Empty,
            Route = notification.Payload.Route,
        };

        await Task.WhenAll(
            targets.Select(target => SendOneAsync(target.Value, notification, payload, ct))
        );
    }

    private static async Task SendOneAsync(
        Client client,
        UserNotification notification,
        NotifyDto payload,
        CancellationToken ct
    )
    {
        try
        {
            await client.Socket.SendAsync(EventName, payload, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.Socket(
                $"SignalR delivery of channel {notification.Channel} to user {notification.UserId} failed: {exception.Message}"
            );
        }
    }
}
