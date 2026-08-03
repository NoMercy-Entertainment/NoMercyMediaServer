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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Authorization;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;

namespace NoMercy.Api.Controllers.V1.Dashboard.Plugins;

/// <summary>
/// The platform's own endpoints for plugin UI, as opposed to the endpoints a
/// plugin owns itself.
/// <para>
/// Authorised for any signed-in user rather than the owner: managing plugins is
/// an owner's job, and using one is not. The owner-only surface stays on
/// <see cref="PluginController"/>.
/// </para>
/// </summary>
[ApiController]
[Tags("Plugin UI")]
[ApiVersion(1.0)]
[Authorize]
public class PluginUiController(IPluginManager pluginManager) : BaseController
{
    /// <summary>
    /// Every plugin the caller's client should show in its navigation.
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/plugins/ui")]
    public IActionResult Discover()
    {
        List<PluginUiDescriptorDto> descriptors = pluginManager
            .GetInstalledPlugins()
            .Where(HasUi)
            .Select(info =>
                PluginUiDescriptorDto.From(
                    info,
                    pluginManager.GetPluginInstance(info.Id) as IUiPlugin
                )
            )
            .ToList();

        return Ok(new DataResponseDto<IEnumerable<PluginUiDescriptorDto>> { Data = descriptors });
    }

    /// <summary>
    /// What a plugin wants rendered for one of its routes.
    /// </summary>
    /// <summary>
    /// The plugin's own strings for one locale.
    ///
    /// A plugin supplies the text in the components it builds, so the client has
    /// no way to translate what it has never seen. Serving them under the
    /// plugin's id lets the client merge them into its own catalogue as a
    /// namespace, which is what keeps two plugins using the same key apart.
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/plugins/{id:guid}/translations/{locale}")]
    public async Task<IActionResult> Translations(Guid id, string locale, CancellationToken ct)
    {
        PluginInfo? info = pluginManager.GetPluginInfo(id);

        if (info is null)
            return NotFoundResponse("Plugin not found");

        // The manager owns the fallback because it is the thing holding the
        // manifest: a viewer whose language the plugin does not ship reads it in
        // the language it was written in, never in empty labels.
        Dictionary<string, string>? strings = await pluginManager.ReadTranslationsAsync(id, locale, ct);

        return Ok(new DataResponseDto<Dictionary<string, string>> { Data = strings ?? [] });
    }

    [HttpGet("api/v{version:apiVersion}/plugins/{id:guid}/view")]
    public async Task<IActionResult> View(
        Guid id,
        [FromQuery] string? route,
        [FromQuery] string? surface,
        CancellationToken ct)
    {
        PluginInfo? info = pluginManager.GetPluginInfo(id);

        if (info is null || !HasUi(info))
            return NotFoundResponse("Plugin not found");

        if (pluginManager.GetPluginInstance(id) is not IUiPlugin plugin)
            return NotFoundResponse("Plugin provides no UI");

        PluginViewRequest request = new()
        {
            Route = string.IsNullOrWhiteSpace(route) ? "/" : route,
            Query = Request
                .Query.Where(entry => entry.Key != "route" && entry.Key != "surface")
                .ToDictionary(entry => entry.Key, entry => entry.Value.ToString()),
            UserId = User.UserId().ToString(),
            // An unknown surface falls back rather than being passed through. A
            // plugin branching on it would hit its own default and serve the
            // desktop shape to a television, which looks like a plugin bug.
            Surface = PluginSurface.IsKnown(surface) ? surface! : PluginSurface.Web,
        };

        try
        {
            PluginView view = await plugin.GetViewAsync(request, ct);
            return Ok(new DataResponseDto<PluginView> { Data = view });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A plugin throwing while building its own screen is that plugin's
            // failure, not a server error, and the client should render an
            // empty panel rather than a 500 the user reads as the server
            // breaking.
            return BadRequestResponse($"Plugin could not render this view: {exception.Message}");
        }
    }

    private static bool HasUi(PluginInfo info) =>
        info.Status == PluginStatus.Active
        && PluginCapabilityGuard.DeclaresHook(info.Capabilities, PluginHookCapability.Ui);
}
