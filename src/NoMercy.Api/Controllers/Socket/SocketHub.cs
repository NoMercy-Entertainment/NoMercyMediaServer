using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.Socket;

public class SocketHub : ConnectionHub
{
    public SocketHub(IHttpContextAccessor httpContextAccessor, IDbContextFactory<MediaContext> contextFactory)
        : base(httpContextAccessor, contextFactory)
    {
    }
}