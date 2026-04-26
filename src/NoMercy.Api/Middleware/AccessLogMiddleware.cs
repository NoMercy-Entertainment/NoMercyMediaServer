using System.Security.Claims;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.SystemCalls;

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
        "/transcode",
        "/manage",
        "/health",
        // SignalR hubs — base path AND /negotiate handshake AND polling
        // fallbacks all share the prefix; exact-match misses /negotiate.
        "/videoHub",
        "/musicHub",
        "/ripperHub",
        "/dashboardHub",
        "/castHub",
    ];

    private readonly string[] _ignoreExact =
    [
        "/",
        "/api/v1/dashboard/logs",
        "/api/v1/dashboard/devices",
        "/api/v1/dashboard/activity",
        // Dashboard overview poll targets — fire every few seconds.
        "/api/v1/dashboard/server",
        "/api/v1/dashboard/server/info",
        "/api/v1/dashboard/server/storage",
        "/api/v1/dashboard/system",
        "/api/v1/dashboard/optical/drives",
        "/api/v1/dashboard/tasks/queue",
        "/api/v1/dashboard/tasks/runners",
        "/api/v1/dashboard/hardware",
        "/api/v1/dashboard/hardware/benchmark",
        // Setup wizard poll targets — fire on every page load.
        "/api/v1/setup/server-info",
        "/api/v1/setup/permissions",
        "/api/v1/setup/libraries",
        "/api/v1/setup/screensaver",
        "/api/v1/setup/music-playlists",
    ];

    private readonly string[] _ignoreIfAuthenticated = [];

    private readonly string[] _ignoreIfGuest = ["/status"];

    public async Task InvokeAsync(HttpContext context)
    {
        string path = HttpUtility.UrlDecode(context.Request.Path);

        bool ignoreStart = _ignoredStartsWithRoutes.Any(route =>
            context.Request.Path.ToString().StartsWith(route)
        );

        bool ignoreExactRoute = _ignoreExact.Any(route =>
            context.Request.Path.ToString().Equals(route)
        );

        // Skip logging for file access paths (folder ID prefix)
        bool isFolderPath = ClaimsPrincipleExtensions.FolderIds.Any(x =>
            path.StartsWith("/" + x, StringComparison.OrdinalIgnoreCase)
        );

        if (ignoreStart || ignoreExactRoute || isFolderPath)
        {
            await _next(context);
            return;
        }

        bool ignoreIfGuest = _ignoreIfGuest.Any(route =>
            context.Request.Path.ToString().Equals(route)
        );

        string? guid = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (guid is null)
        {
            if (ignoreIfGuest)
            {
                await _next(context);
                return;
            }

            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path} (No GUID)");
            await WriteProblemAsync(
                context,
                statusCode: 401,
                type: "https://nomercy.tv/problems/no-token",
                title: "Authentication required",
                detail: "No bearer token was provided. Include a valid JWT in the Authorization header.",
                authError: "NO_TOKEN"
            );
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

            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path} (Empty GUID)");
            await WriteProblemAsync(
                context,
                statusCode: 401,
                type: "https://nomercy.tv/problems/invalid-token",
                title: "Invalid token",
                detail: "The token subject (sub) resolved to an empty GUID. The token may be malformed.",
                authError: "INVALID_TOKEN"
            );
            return;
        }

        bool ignoreIfAuthenticated = _ignoreIfAuthenticated.Any(route =>
            context.Request.Path.ToString().Equals(route)
        );

        if (ignoreIfAuthenticated)
        {
            await _next(context);
            return;
        }

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));
        if (user is null)
        {
            // User cache may not be populated yet during startup — try refreshing from DB
            MediaContext mediaContext = context.RequestServices.GetRequiredService<MediaContext>();
            await ClaimsPrincipleExtensions.RefreshUsersAsync(mediaContext);
            user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));
        }

        if (user is null)
        {
            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path} (User not found)");
            await WriteProblemAsync(
                context,
                statusCode: 401,
                type: "https://nomercy.tv/problems/user-not-found",
                title: "User not found",
                detail: "The authenticated user is not registered on this server. Ask the server owner to add your account.",
                authError: "USER_NOT_FOUND"
            );
            return;
        }

        Logger.Http($"{user.Name}: {path}");

        await _next(context);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string type,
        string title,
        string detail,
        string authError
    )
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        object body = new
        {
            type,
            title,
            status = statusCode,
            detail,
            instance = context.Request.Path.Value,
            authError,
        };

        await context.Response.WriteAsync(JsonConvert.SerializeObject(body), Encoding.UTF8);
    }
}
