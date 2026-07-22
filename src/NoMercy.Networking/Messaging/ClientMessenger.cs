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

namespace NoMercy.Networking.Messaging;

public class ClientMessenger(ConnectedClients connectedClients, ILogger<ClientMessenger> logger)
    : IClientMessenger
{
    // Bounds how long one connection's send can hold up the overall SendTo/SendToAll
    // call. Sequentially awaiting each connection in turn meant one slow or half-dead
    // socket (backgrounded app, half-open TCP connection with no RST yet) delayed
    // delivery to every OTHER connection enumerated after it — up to and including
    // making the caller (e.g. StartPlaybackCommand, still holding its per-user lock)
    // wait out that slow connection before it could even finish handling its own
    // request. Dispatching concurrently and capping each individual send means a
    // dead connection can only ever cost this timeout, never drag the rest with it.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(seconds: 5);

    public async Task SendToAll(string name, string endpoint, object? data = null)
    {
        // ConcurrentDictionary enumeration is a snapshot — safe to iterate
        // while other threads add/remove.
        List<KeyValuePair<string, Client>> targets =
        [
            .. connectedClients.Clients.Where(predicate: client => client.Value.Endpoint == "/" + endpoint),
        ];

        logger.LogDebug(
            message: "SendToAll({Name}, {Endpoint}): {Count} target connection(s)", args: [name, endpoint, targets.Count]
        );

        await Task.WhenAll(
            tasks: targets.Select(selector: target => SendOneAsync(name: name, connectionId: target.Key, client: target.Value, data: data))
        );
    }

    public async Task SendTo(string name, string endpoint, Guid userId, object? data = null)
    {
        List<KeyValuePair<string, Client>> targets =
        [
            .. connectedClients.Clients.Where(predicate: client =>
                client.Value.Sub.Equals(g: userId) && client.Value.Endpoint == "/" + endpoint
            ),
        ];

        logger.LogDebug(
            message: "SendTo({Name}, {Endpoint}, {UserId}): {Count} target connection(s)", args: [name, endpoint, userId, targets.Count]
        );

        await Task.WhenAll(
            tasks: targets.Select(selector: target => SendOneAsync(name: name, connectionId: target.Key, client: target.Value, data: data))
        );
    }

    // Every send runs independently (never sequentially awaited alongside its
    // siblings) and swallows its own failure/timeout so one bad connection can
    // never abort or delay delivery to any other connection in the same broadcast.
    private async Task SendOneAsync(string name, string connectionId, Client client, object? data)
    {
        using CancellationTokenSource cts = new(delay: SendTimeout);
        // Debug-only (not Info): this fires once per connection per broadcast,
        // including the ~5s-cadence position reports every device sends — logging
        // it at Info would spam the run-log. Bump to Debug during a live latency
        // measurement to see exactly which target connection the relay was slow
        // reaching (e.g. a specific device's socket write taking the bulk of the
        // round trip vs. the others).
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (data != null)
                await client.Socket.SendAsync(method: name, arg1: data, cancellationToken: cts.Token);
            else
                await client.Socket.SendAsync(method: name, cancellationToken: cts.Token);

            logger.LogDebug(
                message: "Send {Name} to connection {ConnectionId} (device {DeviceId}) completed in {ElapsedMilliseconds}ms", args: [name, connectionId, client.DeviceId, stopwatch.ElapsedMilliseconds]
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                message: "Send {Name} to connection {ConnectionId} (device {DeviceId}) failed after {ElapsedMilliseconds}ms: {Message}", args: [name, connectionId, client.DeviceId, stopwatch.ElapsedMilliseconds, ex.Message]
            );
        }
    }
}
