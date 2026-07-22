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

namespace NoMercy.Api.Controllers.V1.Dashboard.Plugins;

[ApiController]
[Tags(tags: "Dashboard Server Plugins")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Owner")]
[Route(template: "api/v{version:apiVersion}/dashboard/plugins", Order = 10)]
public class PluginController(IPluginManager pluginManager) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {

        IReadOnlyList<PluginInfo> plugins = pluginManager.GetInstalledPlugins();

        return Ok(
            value: new DataResponseDto<IEnumerable<PluginInfoDto>>
            {
                Data = plugins.Select(selector: p => new PluginInfoDto(info: p)),
            }
        );
    }

    [HttpGet(template: "{id:guid}")]
    public IActionResult Show(Guid id)
    {

        PluginInfo? plugin = pluginManager.GetInstalledPlugins().FirstOrDefault(predicate: p => p.Id == id);
        if (plugin is null)
            return NotFoundResponse(detail: "Plugin not found");

        return Ok(value: new DataResponseDto<PluginInfoDto> { Data = new(info: plugin) });
    }

    [HttpPost(template: "{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id)
    {

        try
        {
            await pluginManager.EnablePluginAsync(pluginId: id);

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Plugin enabled successfully",
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(detail: ex.Message);
        }
    }

    [HttpPost(template: "{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {

        try
        {
            await pluginManager.DisablePluginAsync(pluginId: id);

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Plugin disabled successfully",
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(detail: ex.Message);
        }
    }

    [HttpDelete(template: "{id:guid}")]
    public async Task<IActionResult> Uninstall(Guid id)
    {

        try
        {
            await pluginManager.UninstallPluginAsync(pluginId: id);

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Plugin uninstalled successfully",
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(detail: ex.Message);
        }
    }

    [HttpGet]
    [Route(template: "credentials")]
    public IActionResult Credentials()
    {

        UserPass? aniDb = CredentialManager.Credential(target: "AniDb");

        if (aniDb == null)
            return NotFoundResponse(detail: "No credentials found for AniDb");

        return Ok(
            value: new AniDbCredentialsResponseDto
            {
                Key = "AniDb",
                Username = aniDb.Username,
                ApiKey = aniDb.ApiKey,
            }
        );
    }

    [HttpPost]
    [Route(template: "credentials")]
    public IActionResult Credentials([FromBody] AniDbCredentialsRequestDto requestDto)
    {

        UserPass? aniDb = CredentialManager.Credential(target: requestDto.Key);
        CredentialManager.SetCredentials(
            target: requestDto.Key,
            username: requestDto.Username,
            password: requestDto.Password ?? (aniDb?.Password).OrEmpty(),
            apiKey: requestDto.ApiKey
        );

        return Ok(
            value: new StatusResponseDto<string>
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
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; init; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; init; } = null!;

    [JsonProperty(propertyName: "description")]
    public string Description { get; init; } = null!;

    [JsonProperty(propertyName: "version")]
    public string Version { get; init; } = null!;

    [JsonProperty(propertyName: "status")]
    public string Status { get; init; } = null!;

    [JsonProperty(propertyName: "author")]
    public string? Author { get; init; }

    [JsonProperty(propertyName: "project_url")]
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
