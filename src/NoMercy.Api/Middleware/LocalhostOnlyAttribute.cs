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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NoMercy.Api.Middleware;

[AttributeUsage(validOn: AttributeTargets.Class | AttributeTargets.Method)]
public class LocalhostOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        IPAddress? remoteIp = context.HttpContext.Connection.RemoteIpAddress;

        // Null remote IP happens with named pipes, IPC, and in-process test hosts.
        // The primary security boundary is Kestrel binding to 127.0.0.1 only.
        if (remoteIp is null)
            return;

        bool isLocalhost = IPAddress.IsLoopback(address: remoteIp);

        if (!isLocalhost)
        {
            context.Result = new JsonResult(
                value: new
                {
                    status = "error",
                    message = "Management API is only accessible from localhost",
                }
            )
            {
                StatusCode = 403,
            };
        }
    }
}
