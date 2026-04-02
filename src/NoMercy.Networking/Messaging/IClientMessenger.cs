namespace NoMercy.Networking.Messaging;

public interface IClientMessenger
{
    Task SendToAll(string name, string endpoint, object? data = null);
    Task SendTo(string name, string endpoint, Guid userId, object? data = null);
}
