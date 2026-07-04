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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace NoMercy.Service.Authorization;

/// <summary>
/// Renders policy authorization failures as the same ProblemDetails 403 the
/// controllers' imperative permission checks used to return, so moving an
/// endpoint onto <c>[Authorize(Policy = ...)]</c> keeps its forbidden response
/// shape consistent. The authentication-challenge (401) path is left to the
/// framework default. Only the Forbidden path is customised, so endpoints that
/// have not been migrated are unaffected.
/// </summary>
public class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult
    )
    {
        if (authorizeResult.Forbidden)
        {
            ProblemDetails problem = new()
            {
                Type = "/docs/errors/forbidden",
                Title = "Unauthorized.",
                Detail = "You do not have permission to access this resource.",
                Instance = context.Request.Path,
                Status = StatusCodes.Status403Forbidden,
                Extensions = { { "traceId", context.TraceIdentifier } },
            };
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
