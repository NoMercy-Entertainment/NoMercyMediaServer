using Microsoft.AspNetCore.SignalR;

namespace NoMercy.Networking.Messaging;

public class ClientMessenger(ConnectedClients connectedClients) : IClientMessenger
{
    public bool SendToAll(string name, string endpoint, object? data = null)
    {
        foreach ((string _, Client client) in connectedClients.Clients.Where(client => client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    client.Socket.SendAsync(name, data).Wait();
                else
                    client.Socket.SendAsync(name).Wait();
            }
            catch (Exception)
            {
                return false;
            }

        return true;
    }

    public async Task SendTo(string name, string endpoint, Guid userId, object? data = null)
    {
        foreach ((string _, Client client) in connectedClients.Clients.Where(client =>
                     client.Value.Sub.Equals(userId) && client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    await client.Socket.SendAsync(name, data);
                else
                    await client.Socket.SendAsync(name);
            }
            catch (Exception)
            {
                return;
            }

        await Task.CompletedTask;
    }
}
