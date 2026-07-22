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

using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.Services;
using NoMercy.Authorization;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Configuration;

namespace NoMercy.Api.Middleware;

public class TokenParamAuthMiddleware(
    RequestDelegate next,
    ILiveIngestKeyStore ingestKeyStore,
    ILogger<TokenParamAuthMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Loopback self-ingest: ffmpeg/ffprobe pull a library source over the
        // internal serving port with a single-use ingest key scoped to one file,
        // in place of the viewer's bearer. Honoured only for the exact file the
        // key was minted for, and only when the request actually arrived on the
        // loopback-only ingest listener (InternalServerPort + 1). Gating on the
        // OS-bound local port — not just a loopback source IP — closes the case
        // where a local relay (Cloudflare Tunnel's cloudflared) forwards external
        // traffic to the PUBLIC port from 127.0.0.1: that traffic never lands on
        // the ingest port, so it can never reach this bypass.
        if (
            context.Connection.LocalPort == RuntimeServerSettings.Current.InternalServerPort + 1
            && context.Connection.RemoteIpAddress is { } remoteIp
            && IPAddress.IsLoopback(address: remoteIp)
        )
        {
            string ingestKey = context.Request.Headers[key: "X-NoMercy-Ingest-Key"].ToString();
            if (
                !string.IsNullOrEmpty(value: ingestKey)
                && ingestKeyStore.TryValidate(key: ingestKey, requestPath: context.Request.Path.Value ?? string.Empty)
            )
            {
                await next(context: context);
                return;
            }
        }

        context.Request.Headers.Authorization = context
            .Request.Headers.Authorization.ToString()
            .Split(separator: ",")
            .ElementAt(index: 0)
            .Split(separator: "&")
            .ElementAt(index: 0);

        // Extract JWT from query params for all requests (enables ?token= and ?access_token= everywhere)
        if (!context.Request.Headers.Authorization.ToString().Contains(value: "Bearer"))
        {
            string jwt = context
                .Request.Query.FirstOrDefault(predicate: q => q.Key is "token" or "access_token")
                .Value.ToString();

            if (!string.IsNullOrEmpty(value: jwt))
            {
                context.Request.Headers.Authorization = new(value: "Bearer " + jwt);
            }
        }

        string url = context.Request.Path;

        if (
            !UserCache.Current.FolderIds.Any(predicate: x => url.StartsWith(value: "/" + x))
            || context.Request.Headers.Authorization.ToString().Contains(value: "Bearer")
        )
        {
            await next(context: context);
            return;
        }

        string? claim = context.User.FindFirstValue(claimType: ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(value: claim))
        {
            logger.LogInformation(message: "Unauthorized request, no jwt: {Url}", args: url);
            await WriteProblemAsync(
                context: context,
                statusCode: (int)HttpStatusCode.Unauthorized,
                type: "https://nomercy.tv/problems/no-token",
                title: "Authentication required",
                detail: "No bearer token was provided. Include a valid JWT in the Authorization header or as an access_token query parameter.",
                authError: "NO_TOKEN"
            );
            return;
        }

        if (!Guid.TryParse(input: claim, result: out Guid userId) || userId == Guid.Empty)
        {
            logger.LogInformation(message: "Unauthorized request, guid malformed or empty: {Url}", args: url);
            await WriteProblemAsync(
                context: context,
                statusCode: (int)HttpStatusCode.Forbidden,
                type: "https://nomercy.tv/problems/invalid-token",
                title: "Invalid token",
                detail: "The token subject (sub) is not a valid GUID. The token may be malformed.",
                authError: "INVALID_TOKEN"
            );
            return;
        }

        User? user = UserCache.Current.Users.FirstOrDefault(predicate: x => x.Id.Equals(g: userId));

        if (user is null)
        {
            logger.LogInformation(message: "Unauthorized request, user not found: {Url}", args: url);
            await WriteProblemAsync(
                context: context,
                statusCode: (int)HttpStatusCode.Forbidden,
                type: "https://nomercy.tv/problems/user-not-found",
                title: "User not found",
                detail: "The authenticated user is not registered on this server. Ask the server owner to add your account.",
                authError: "USER_NOT_FOUND"
            );
            return;
        }

        await next(context: context);
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
