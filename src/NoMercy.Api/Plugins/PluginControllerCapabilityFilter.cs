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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Mvc;

namespace NoMercy.Api.Plugins;

/// <summary>
/// The one place a plugin's REST surface is allowed or refused.
/// <para>
/// Detaching the application part on disable already removes the routes, but
/// the descriptor cache is refreshed asynchronously and a request in flight can
/// still land on a stale descriptor. This check runs per request against
/// current state, so the window is closed rather than narrowed.
/// </para>
/// <para>
/// A refused request is a 404 and not a 403: whether a plugin is installed is
/// not something an unauthorised caller should be able to probe for.
/// </para>
/// </summary>
public class PluginControllerCapabilityFilter(IPluginManager pluginManager) : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is not PluginControllerBase)
            return next();

        if (!Ulid.TryParse(context.RouteData.Values["pluginId"]?.ToString(), out Ulid pluginId))
        {
            context.Result = new NotFoundResult();
            return Task.CompletedTask;
        }

        PluginInfo? info = pluginManager.GetPluginInfo(pluginId);

        if (info is null || info.Status != PluginStatus.Active || info.Capabilities?.Rest != true)
        {
            context.Result = new NotFoundResult();
            return Task.CompletedTask;
        }

        return next();
    }
}
