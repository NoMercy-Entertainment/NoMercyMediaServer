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
using Microsoft.Extensions.Logging;
using NoMercy.Networking.Http;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Networking.Messaging;

public class ClientMessenger(ConnectedClients connectedClients, ILogger<ClientMessenger> logger)
    : IClientMessenger
{
    public async Task SendToAll(string name, string endpoint, object? data = null)
    {
        // ConcurrentDictionary enumeration is a snapshot — safe to iterate
        // while other threads add/remove. Continue past per-client failures
        // so one disconnected receiver doesn't stop delivery to the rest;
        // the previous 'return' would silently drop every client after the
        // first failure on every broadcast.
        foreach (
            (string _, Client client) in connectedClients.Clients.Where(client =>
                client.Value.Endpoint == "/" + endpoint
            )
        )
        {
            try
            {
                if (data != null)
                    await client.Socket.SendAsync(name, data);
                else
                    await client.Socket.SendAsync(name);
            }
            catch (Exception ex)
            {
                logger.LogDebug($"Broadcast to client failed: {ex.Message}");
                continue;
            }
        }
    }

    public async Task SendTo(string name, string endpoint, Guid userId, object? data = null)
    {
        foreach (
            (string _, Client client) in connectedClients.Clients.Where(client =>
                client.Value.Sub.Equals(userId) && client.Value.Endpoint == "/" + endpoint
            )
        )
        {
            try
            {
                if (data != null)
                    await client.Socket.SendAsync(name, data);
                else
                    await client.Socket.SendAsync(name);
            }
            catch (Exception ex)
            {
                logger.LogDebug($"Broadcast to client failed: {ex.Message}");
                continue;
            }
        }

        await Task.CompletedTask;
    }
}
