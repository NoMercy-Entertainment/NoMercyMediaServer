using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DnsClient;

namespace NoMercy.NmSystem.Extensions;

public static class HttpClient
{
    private static readonly ConcurrentDictionary<string, LookupClient> DnsClients = new();

    public static System.Net.Http.HttpClient WithDns(string? dnsServer = null)
    {
        string server = dnsServer ?? Information.Config.DnsServer;
        SocketsHttpHandler handler = new()
        {
            ConnectCallback = async (context, token) =>
            {
                IPHostEntry hostEntry;
                if (!string.IsNullOrEmpty(server))
                {
                    LookupClient dnsClient = DnsClients.GetOrAdd(server, s => new(IPAddress.Parse(s)));
                    IDnsQueryResponse? result = await dnsClient.QueryAsync(context.DnsEndPoint.Host, QueryType.A,
                        cancellationToken: token);
                    IPAddress? address = result.Answers.ARecords().FirstOrDefault()?.Address;
                    if (address == null) throw new SocketException((int)SocketError.HostNotFound);

                    hostEntry = new()
                    {
                        AddressList = [address]
                    };
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
            }
        };

        return new(handler);
    }
}