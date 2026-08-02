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

using Microsoft.AspNetCore.Http;

namespace NoMercy.Api.Middleware;

/// <summary>
/// Answers the Private Network Access (a.k.a. Local Network Access) CORS
/// preflight. When a page served from a public origin (app.nomercy.tv) opens a
/// request to a server on a private address — the common case where a self-hosted
/// server's registered hostname resolves to its LAN IP — Chrome and Firefox send
/// the preflight with <c>Access-Control-Request-Private-Network: true</c> and
/// require the server to opt in with <c>Access-Control-Allow-Private-Network:
/// true</c>. ASP.NET Core's CORS middleware has no built-in support for this
/// response header, so it is set here, ahead of <c>UseCors</c>, on every
/// preflight that asks for it. Without it a modern browser kills the connection
/// (Firefox blocks outright, Chrome is on a phased rollout), which shows up as
/// "CORS request did not succeed" and a hub reconnect storm.
/// <para>
/// Chrome renamed the feature to Local Network Access and renamed the header pair
/// with it, so a browser on the newer build asks with
/// <c>Access-Control-Request-Local-Network-Access</c> and never sees the older
/// answer. Both spellings are answered because the two live side by side across
/// browser versions. The rename is only half of it: the newer model also gates the
/// request on a user permission, and a denied prompt fails the fetch with
/// "Permission was denied for this request to access the `local` address space"
/// no matter what this middleware returns.
/// </para>
/// </summary>
public class PrivateNetworkAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            if (AsksFor(context, "Access-Control-Request-Private-Network"))
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";

            if (AsksFor(context, "Access-Control-Request-Local-Network-Access"))
                context.Response.Headers["Access-Control-Allow-Local-Network-Access"] = "true";
        }

        await next(context);
    }

    private static bool AsksFor(HttpContext context, string header)
    {
        return string.Equals(
            context.Request.Headers[header],
            "true",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
