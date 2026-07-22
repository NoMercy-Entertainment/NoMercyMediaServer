// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Security.Claims;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Middleware;

public class AccessLogMiddleware
{
    private readonly RequestDelegate _next;

    private readonly ILogger<AccessLogMiddleware> _logger;

    public AccessLogMiddleware(RequestDelegate next, ILogger<AccessLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
    ];

    private readonly string[] _ignoreIfAuthenticated = [];

    private readonly string[] _ignoreIfGuest = ["/status"];

    public async Task InvokeAsync(HttpContext context)
    {
        string path = HttpUtility.UrlDecode(str: context.Request.Path);

        bool ignoreStart = _ignoredStartsWithRoutes.Any(predicate: route =>
            context.Request.Path.ToString().StartsWith(value: route)
        );

        bool ignoreExactRoute = _ignoreExact.Any(predicate: route =>
            context.Request.Path.ToString().Equals(value: route)
        );

        // Skip logging for file access paths (folder ID prefix)
        bool isFolderPath = UserCache.Current.FolderIds.Any(predicate: x =>
            path.StartsWith(value: "/" + x, comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        if (ignoreStart || ignoreExactRoute || isFolderPath)
        {
            await _next(context: context);
            return;
        }

        bool ignoreIfGuest = _ignoreIfGuest.Any(predicate: route =>
            context.Request.Path.ToString().Equals(value: route)
        );

        string? guid = context.User.FindFirstValue(claimType: ClaimTypes.NameIdentifier);
        if (guid is null)
        {
            if (ignoreIfGuest)
            {
                await _next(context: context);
                return;
            }

            _logger.LogInformation(
                message: "Unknown: {RemoteIpAddress}: {Path} (No GUID)", args: [context.Connection.RemoteIpAddress, path]
            );
            await WriteProblemAsync(
                context: context,
                statusCode: 401,
                type: "https://nomercy.tv/problems/no-token",
                title: "Authentication required",
                detail: "No bearer token was provided. Include a valid JWT in the Authorization header.",
                authError: "NO_TOKEN"
            );
            return;
        }

        if (!Guid.TryParse(input: guid, result: out Guid userId) || userId == Guid.Empty)
        {
            if (ignoreIfGuest)
            {
                await _next(context: context);
                return;
            }

            _logger.LogInformation(
                message: "Unknown: {RemoteIpAddress}: {Path} (Malformed or empty GUID)", args: [context.Connection.RemoteIpAddress, path]
            );
            await WriteProblemAsync(
                context: context,
                statusCode: 401,
                type: "https://nomercy.tv/problems/invalid-token",
                title: "Invalid token",
                detail: "The token subject (sub) is not a valid GUID. The token may be malformed.",
                authError: "INVALID_TOKEN"
            );
            return;
        }

        bool ignoreIfAuthenticated = _ignoreIfAuthenticated.Any(predicate: route =>
            context.Request.Path.ToString().Equals(value: route)
        );

        if (ignoreIfAuthenticated)
        {
            await _next(context: context);
            return;
        }

        User? user = UserCache.Current.Users.FirstOrDefault(predicate: x => x.Id.Equals(g: userId));
        if (user is null)
        {
            // User cache may not be populated yet during startup — try refreshing from DB
            MediaContext mediaContext = context.RequestServices.GetRequiredService<MediaContext>();
            await UserCache.Current.RefreshUsersAsync(context: mediaContext);
            user = UserCache.Current.Users.FirstOrDefault(predicate: x => x.Id.Equals(g: userId));
        }

        if (user is null)
        {
            _logger.LogInformation(
                message: "Unknown: {RemoteIpAddress}: {Path} (User not found)", args: [context.Connection.RemoteIpAddress, path]
            );
            await WriteProblemAsync(
                context: context,
                statusCode: 401,
                type: "https://nomercy.tv/problems/user-not-found",
                title: "User not found",
                detail: "The authenticated user is not registered on this server. Ask the server owner to add your account.",
                authError: "USER_NOT_FOUND"
            );
            return;
        }

        // Per-request access logging is Debug, not Information: a dashboard or
        // client polls dozens of endpoints every few seconds, so logging every
        // authenticated request at Information floods the console and drowns real
        // events. The whack-a-mole ignore lists above only ever caught the
        // pollers someone noticed. Auth failures above stay at Information — those
        // are rare and worth surfacing.
        _logger.LogDebug(message: "{Name}: {Path}", args: [user.Name, path]);

        await _next(context: context);
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

        await context.Response.WriteAsync(text: JsonConvert.SerializeObject(value: body), encoding: Encoding.UTF8);
    }
}
