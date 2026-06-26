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

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DnsClient;
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.Extensions;

public static class HttpClientExtensions
{
    private static readonly ConcurrentDictionary<string, LookupClient> DnsClients = new();

    public static HttpClient WithNoMercyUserAgent(
        this HttpClient client
    )
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
        return client;
    }

    public static HttpClient WithDns(string? dnsServer = null)
    {
        string server = dnsServer ?? Config.DnsServer;
        SocketsHttpHandler handler = new()
        {
            ConnectCallback = async (context, token) =>
            {
                IPHostEntry hostEntry;
                if (!string.IsNullOrEmpty(server))
                {
                    LookupClient dnsClient = DnsClients.GetOrAdd(
                        server,
                        s => new(IPAddress.Parse(s))
                    );
                    IDnsQueryResponse? result = await dnsClient.QueryAsync(
                        context.DnsEndPoint.Host,
                        QueryType.A,
                        cancellationToken: token
                    );
                    IPAddress? address = result.Answers.ARecords().FirstOrDefault()?.Address;
                    if (address == null)
                        throw new SocketException((int)SocketError.HostNotFound);

                    hostEntry = new() { AddressList = [address] };
                }
                else
                {
                    hostEntry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, token);
                }

                IPEndPoint endpoint = new(hostEntry.AddressList[0], context.DnsEndPoint.Port);
                Socket socket = new(SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    await socket.ConnectAsync(endpoint, token);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        return new(handler);
    }
}
