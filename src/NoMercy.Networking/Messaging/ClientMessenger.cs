using Microsoft.AspNetCore.SignalR;

namespace NoMercy.Networking.Messaging;

public class ClientMessenger(ConnectedClients connectedClients) : IClientMessenger
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                continue;
            }
        }

        await Task.CompletedTask;
    }
}
