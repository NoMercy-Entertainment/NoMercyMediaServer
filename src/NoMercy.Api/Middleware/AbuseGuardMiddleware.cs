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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Security;
using NoMercy.Networking.Http;

namespace NoMercy.Api.Middleware;

/// <summary>
/// Rejects a banned caller on the way in, scores the outcome on the way out.
/// </summary>
/// <remarks>
/// Registered BEFORE UseRouting so routing runs inside the call to next: by the
/// time control comes back, GetEndpoint tells us whether this server serves the
/// requested path at all, which is the signal a scanner cannot avoid producing.
/// </remarks>
public class AbuseGuardMiddleware(RequestDelegate next, ILogger<AbuseGuardMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAbuseGuard guard)
    {
        IPAddress? address = context.ClientIp();

        if (await guard.IsBannedAsync(address, context.RequestAborted))
        {
            logger.LogDebug("Rejected banned {Address} on {Path}", address, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // A request that threw is a server fault, not evidence about the caller,
        // so the exception unwinds without scoring anything.
        await next(context);

        RequestOutcome outcome = new(
            context.Request.Path.ToString(),
            context.GetEndpoint() is not null,
            context.Response.StatusCode,
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not null
        );

        await guard.RecordAsync(address, outcome, context.RequestAborted);
    }
}
