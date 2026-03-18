using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NoMercy.Api.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class LocalhostOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        IPAddress? remoteIp = context.HttpContext.Connection.RemoteIpAddress;

        // Null remote IP happens with named pipes, IPC, and in-process test hosts.
        // The primary security boundary is Kestrel binding to 127.0.0.1 only.
        if (remoteIp is null)
            return;

        bool isLocalhost = IPAddress.IsLoopback(remoteIp);

        if (!isLocalhost)
        {
            context.Result = new JsonResult(new { status = "error", message = "Management API is only accessible from localhost" })
            {
                StatusCode = 403
            };
        }
    }
}
