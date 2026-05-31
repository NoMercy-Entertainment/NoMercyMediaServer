using System.Collections.Concurrent;
using NoMercy.Networking.Http;

namespace NoMercy.Networking.Messaging;

public class ConnectedClients
{
    public ConcurrentDictionary<string, Client> Clients { get; } = new();
}
