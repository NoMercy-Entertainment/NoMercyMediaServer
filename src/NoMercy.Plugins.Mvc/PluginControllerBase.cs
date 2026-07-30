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

namespace NoMercy.Plugins.Mvc;

/// <summary>
/// What a plugin's REST controllers inherit.
/// <para>
/// It derives from <see cref="ControllerBase"/> rather than from the server's
/// own <c>BaseController</c>: that one carries the authorization policy, the
/// user cache and the whole DTO surface, none of which a plugin should be
/// binding against, and all of which would have to become part of the shared
/// contract to make the inheritance legal.
/// </para>
/// <para>
/// The envelope helpers are here so a plugin's responses look like every other
/// response the clients already parse, without the plugin naming the server's
/// DTO types. A serialisation test in the server's suite is what keeps the two
/// shapes identical.
/// </para>
/// </summary>
[ApiController]
public abstract class PluginControllerBase : ControllerBase
{
    /// <summary>
    /// Which plugin this request was routed to, taken from the route the
    /// convention prefixed. <see cref="Guid.Empty"/> when a controller is
    /// constructed outside a request, as in a unit test.
    /// </summary>
    protected Guid PluginId =>
        Guid.TryParse(RouteData.Values["pluginId"]?.ToString(), out Guid id) ? id : Guid.Empty;

    /// <summary>A payload, in the <c>{ data }</c> envelope.</summary>
    protected OkObjectResult Data<T>(T data) => Ok(new PluginDataResponse<T> { Data = data });

    /// <summary>
    /// An outcome, in the <c>{ status, data, message, args }</c> envelope.
    /// <para>
    /// <paramref name="message"/> is not localised here. The server localises
    /// its own messages against its own resource files, and a plugin's strings
    /// are not in them — a plugin ships whatever language it resolved.
    /// </para>
    /// </summary>
    protected OkObjectResult Status<T>(T data, string status = "ok", string? message = null) =>
        Ok(
            new PluginStatusResponse<T>
            {
                Status = status,
                Data = data,
                Message = message,
            }
        );

    /// <summary>A page, in the <c>{ data, next_page, has_more }</c> envelope.</summary>
    protected OkObjectResult Paginated<T>(IEnumerable<T> data, int? nextPage) =>
        Ok(
            new PluginPaginatedResponse<T>
            {
                Data = data,
                NextPage = nextPage,
                HasMore = nextPage is not null,
            }
        );
}
