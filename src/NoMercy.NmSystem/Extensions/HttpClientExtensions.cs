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
using NoMercy.NmSystem.Configuration;

namespace NoMercy.NmSystem.Extensions;

public static class HttpClientExtensions
{
    private const string DefaultDnsServer = "1.1.1.1";

    private static readonly ConcurrentDictionary<string, LookupClient> DnsClients = new();

    public static HttpClient WithNoMercyUserAgent(this HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(input: ExternalServicesConfig.Current.UserAgent);
        return client;
    }

    public static SocketsHttpHandler CreateDnsHandler(string? dnsServer = null)
    {
        string server = dnsServer ?? DefaultDnsServer;
        return new()
        {
            ConnectCallback = async (context, token) =>
            {
                IPHostEntry hostEntry;
                if (!string.IsNullOrEmpty(value: server))
                {
                    LookupClient dnsClient = DnsClients.GetOrAdd(
                        key: server,
                        valueFactory: s => new(nameServers: IPAddress.Parse(ipString: s))
                    );
                    IDnsQueryResponse? result = await dnsClient.QueryAsync(
                        query: context.DnsEndPoint.Host,
                        queryType: QueryType.A,
                        cancellationToken: token
                    );
                    IPAddress? address = result.Answers.ARecords().FirstOrDefault()?.Address;
                    if (address == null)
                        throw new SocketException(errorCode: (int)SocketError.HostNotFound);
                    hostEntry = new() { AddressList = [address] };
                }
                else
                {
                    hostEntry = await Dns.GetHostEntryAsync(hostNameOrAddress: context.DnsEndPoint.Host, cancellationToken: token);
                }

                IPEndPoint endpoint = new(address: hostEntry.AddressList[0], port: context.DnsEndPoint.Port);
                Socket socket = new(socketType: SocketType.Stream, protocolType: ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(remoteEP: endpoint, cancellationToken: token);
                    return new NetworkStream(socket: socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }

    public static HttpClient WithDns(string? dnsServer = null) => new(handler: CreateDnsHandler(dnsServer: dnsServer));
}
