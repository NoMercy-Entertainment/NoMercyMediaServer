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
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Extensions;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;

namespace NoMercy.Api.Controllers.V1.Dashboard.Plugins;

[ApiController]
[Tags("Dashboard Server Plugins")]
[ApiVersion(1.0)]
[Authorize(Policy = "Owner")]
[Route("api/v{version:apiVersion}/dashboard/plugins", Order = 10)]
public class PluginController(
    IPluginManager pluginManager,
    IPluginConsentService consentService,
    IPluginGrantStore grantStore
) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        IReadOnlyList<PluginInfo> plugins = pluginManager.GetInstalledPlugins();

        return Ok(
            new DataResponseDto<IEnumerable<PluginInfoDto>>
            {
                Data = plugins.Select(p => new PluginInfoDto(p)),
            }
        );
    }

    [HttpGet("{id:guid}")]
    public IActionResult Show(Guid id)
    {
        PluginInfo? plugin = pluginManager.GetInstalledPlugins().FirstOrDefault(p => p.Id == id);
        if (plugin is null)
            return NotFoundResponse("Plugin not found");

        return Ok(new DataResponseDto<PluginInfoDto> { Data = new(plugin) });
    }

    /// <summary>
    /// Records the owner's consent to a plugin's declared capabilities, then
    /// enables it.
    /// <para>
    /// An elevated plugin — anything declaring rest, ws, network or an elevated
    /// hook — installs disabled and cannot enable itself. That part was right;
    /// what was missing is this. <c>GrantConsent</c> existed with no caller, so
    /// "installs disabled pending consent" was a dead end rather than a state
    /// with a way out, and every plugin needing outbound access was stuck at
    /// first run.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/consent")]
    public async Task<IActionResult> Consent(Guid id, [FromBody] PluginConsentRequestDto? request)
    {
        PluginInfo? plugin = pluginManager.GetInstalledPlugins().FirstOrDefault(p => p.Id == id);
        if (plugin is null)
            return NotFoundResponse("Plugin not found");

        consentService.GrantConsent(id);

        // Grants named in the same call, so consenting to a plugin that needs a
        // library or a host is one decision for the owner rather than three
        // prompts they learn to click through.
        foreach (PluginGrantDto grant in request?.Grants ?? [])
        {
            if (string.IsNullOrWhiteSpace(grant.Kind) || string.IsNullOrWhiteSpace(grant.Value))
                continue;

            grantStore.Grant(id, grant.Kind, grant.Value);
        }

        try
        {
            await pluginManager.EnablePluginAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }

        return Ok(
            new StatusResponseDto<string> { Status = "ok", Message = "Plugin consent granted" }
        );
    }

    /// <summary>Withdraws consent and disables the plugin.</summary>
    [HttpDelete("{id:guid}/consent")]
    public async Task<IActionResult> RevokeConsent(Guid id)
    {
        consentService.RevokeConsent(id);

        foreach (string kind in AllGrantKinds)
        foreach (string value in grantStore.Granted(id, kind))
            grantStore.Revoke(id, kind, value);

        try
        {
            await pluginManager.DisablePluginAsync(id);
        }
        catch (InvalidOperationException)
        {
            // Already gone or never loaded. The consent record is what this
            // route is responsible for, and that is now withdrawn.
        }

        return Ok(
            new StatusResponseDto<string> { Status = "ok", Message = "Plugin consent revoked" }
        );
    }

    /// <summary>Everything plugins have asked the owner for and not yet been given.</summary>
    [HttpGet("grants/pending")]
    public IActionResult PendingGrants() =>
        Ok(
            new DataResponseDto<IEnumerable<PluginGrantRequestDto>>
            {
                Data = grantStore
                    .PendingRequests()
                    .Select(request => new PluginGrantRequestDto(request)),
            }
        );

    /// <summary>Answers one pending request. Denying clears it rather than recording a denial.</summary>
    [HttpPost("{id:guid}/grants")]
    public IActionResult ResolveGrant(Guid id, [FromBody] PluginGrantDecisionDto decision)
    {
        if (string.IsNullOrWhiteSpace(decision.Kind) || string.IsNullOrWhiteSpace(decision.Value))
            return UnprocessableEntityResponse("A grant needs both a kind and a value");

        if (decision.Granted)
            grantStore.Grant(id, decision.Kind, decision.Value);
        else
            grantStore.ClearRequest(id, decision.Kind, decision.Value);

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "ok",
                Message = decision.Granted ? "Grant given" : "Grant denied",
            }
        );
    }

    private static readonly string[] AllGrantKinds =
    [
        PluginGrantKind.Capability,
        PluginGrantKind.NetworkHost,
        PluginGrantKind.LibraryWrite,
    ];

    [HttpPost("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id)
    {
        try
        {
            await pluginManager.EnablePluginAsync(id);

            return Ok(
                new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Plugin enabled successfully",
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {
        try
        {
            await pluginManager.DisablePluginAsync(id);

            return Ok(
                new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Plugin disabled successfully",
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Uninstall(Guid id)
    {
        try
        {
            await pluginManager.UninstallPluginAsync(id);

            return Ok(
                new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Plugin uninstalled successfully",
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    [HttpGet]
    [Route("credentials")]
    public IActionResult Credentials()
    {
        UserPass? aniDb = CredentialManager.Credential("AniDb");

        if (aniDb == null)
            return NotFoundResponse("No credentials found for AniDb");

        return Ok(
            new AniDbCredentialsResponseDto
            {
                Key = "AniDb",
                Username = aniDb.Username,
                ApiKey = aniDb.ApiKey,
            }
        );
    }

    [HttpPost]
    [Route("credentials")]
    public IActionResult Credentials([FromBody] AniDbCredentialsRequestDto requestDto)
    {
        UserPass? aniDb = CredentialManager.Credential(requestDto.Key);
        CredentialManager.SetCredentials(
            requestDto.Key,
            requestDto.Username,
            requestDto.Password ?? (aniDb?.Password).OrEmpty(),
            requestDto.ApiKey
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Credentials set successfully for {0}",
                Args = [requestDto.Key],
            }
        );
    }
}

public record PluginInfoDto
{
    [JsonProperty("id")]
    public Guid Id { get; init; }

    [JsonProperty("name")]
    public string Name { get; init; } = null!;

    [JsonProperty("description")]
    public string Description { get; init; } = null!;

    [JsonProperty("version")]
    public string Version { get; init; } = null!;

    [JsonProperty("status")]
    public string Status { get; init; } = null!;

    [JsonProperty("author")]
    public string? Author { get; init; }

    [JsonProperty("project_url")]
    public string? ProjectUrl { get; init; }

    public PluginInfoDto() { }

    public PluginInfoDto(PluginInfo info)
    {
        Id = info.Id;
        Name = info.Name;
        Description = info.Description;
        Version = info.Version.ToString();
        Status = info.Status.ToString().ToLowerInvariant();
        Author = info.Author;
        ProjectUrl = info.ProjectUrl;
    }
}
