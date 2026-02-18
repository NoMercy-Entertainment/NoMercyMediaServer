using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Controllers.Socket;

public class SocketHub : ConnectionHub
{
    public SocketHub(IHttpContextAccessor httpContextAccessor, IDbContextFactory<MediaContext> contextFactory, ConnectedClients connectedClients)
        : base(httpContextAccessor, contextFactory, connectedClients)
    {
    }
}