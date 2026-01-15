using Microsoft.AspNetCore.Http;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.Socket;

public class SocketHub : ConnectionHub
{
    public SocketHub(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
    {
    }
}