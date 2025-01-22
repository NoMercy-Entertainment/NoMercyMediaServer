using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Http;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;

namespace NoMercy.Api.Middleware;

public class AccessLogMiddleware
{
    private readonly RequestDelegate _next;
    public AccessLogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    private readonly string[] _ignoredStartsWithRoutes =
    [
        "/images",
        "/swagger",
        "/index",
        "/oauth2",
        "/styles",
        "/scripts",
        "/favicon",
    ];

    private readonly string[] _ignoreExact =
    [
        "/",
        "/api/v1/dashboard/logs"
    ];

    private readonly string[] _ignoreIfAuthenticated =
    [
        "/socket"
    ];

    private readonly string[] _ignoreIfGuest =
    [
        "/status"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        string path = HttpUtility.UrlDecode(context.Request.Path);
        
        bool ignoreStart = _ignoredStartsWithRoutes
            .Any(route => context.Request.Path.ToString().StartsWith(route));

        bool ignoreExactRoute = _ignoreExact
            .Any(route => context.Request.Path.ToString().Equals(route));

        if (ignoreStart || ignoreExactRoute)
        {
            await _next(context);
            return;
        }

        bool ignoreIfGuest = _ignoreIfGuest
            .Any(route => context.Request.Path.ToString().Equals(route));

        string? guid = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (guid is null)
        {
            if (ignoreIfGuest)
            {
                await _next(context);
                return;
            }

            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path}");
            context.Response.StatusCode = 401;
            // await context.Response.WriteAsync("Unauthorized");
            return;
        }


        Guid userId = Guid.Parse(guid);
        if (userId == Guid.Empty)
        {
            if (ignoreIfGuest)
            {
                await _next(context);
                return;
            }

            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path}");
            context.Response.StatusCode = 401;
            // await context.Response.WriteAsync("Unauthorized");
            return;
        }

        bool ignoreIfAuthenticated = _ignoreIfAuthenticated
            .Any(route => context.Request.Path.ToString().Equals(route));

        if (ignoreIfAuthenticated)
        {
            await _next(context);
            return;
        }

        if (ClaimsPrincipleExtensions.FolderIds.Any(x => path.StartsWith("/" + x)))
        {
            await _next(context);
            return;
        }

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));
        if (user is null)
        {
            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path}");
            context.Response.StatusCode = 401;
            // await context.Response.WriteAsync("Unauthorized");
            return;
        }

        Logger.Http($"{user?.Name ?? $": {context.Connection.RemoteIpAddress}:"}: {path}");

        await _next(context);
    }
}
