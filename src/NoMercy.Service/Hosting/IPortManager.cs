using Microsoft.AspNetCore.Http;

namespace NoMercy.Service.Hosting;

public interface IPortManager
{
    Task EnsurePortAvailable(int port);
    int FindNextAvailablePort(int startPort);
    bool IsPortAvailable(int port);
    Task<bool> HandlePortInUse(int port, IOException ex);
}
