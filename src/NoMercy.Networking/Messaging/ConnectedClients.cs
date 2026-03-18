using System.Collections.Concurrent;

namespace NoMercy.Networking.Messaging;

public class ConnectedClients
{
    public ConcurrentDictionary<string, Client> Clients { get; } = new();
}
