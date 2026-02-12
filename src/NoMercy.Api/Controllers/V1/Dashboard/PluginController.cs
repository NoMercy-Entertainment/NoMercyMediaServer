using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Common;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Plugins")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/plugins", Order = 10)]
public class PluginController(IPluginManager pluginManager) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view plugins");

        IReadOnlyList<PluginInfo> plugins = pluginManager.GetInstalledPlugins();

        return Ok(new DataResponseDto<IEnumerable<PluginInfoDto>>
        {
            Data = plugins.Select(p => new PluginInfoDto(p))
        });
    }

    [HttpGet("{id:guid}")]
    public IActionResult Show(Guid id)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view plugins");

        PluginInfo? plugin = pluginManager.GetInstalledPlugins().FirstOrDefault(p => p.Id == id);
        if (plugin is null)
            return NotFoundResponse("Plugin not found");

        return Ok(new DataResponseDto<PluginInfoDto>
        {
            Data = new PluginInfoDto(plugin)
        });
    }

    [HttpPost("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to manage plugins");

        try
        {
            await pluginManager.EnablePluginAsync(id);

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Plugin enabled successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to manage plugins");

        try
        {
            await pluginManager.DisablePluginAsync(id);

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Plugin disabled successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Uninstall(Guid id)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to manage plugins");

        try
        {
            await pluginManager.UninstallPluginAsync(id);

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Plugin uninstalled successfully"
            });
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
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view credentials");

        UserPass? aniDb = CredentialManager.Credential("AniDb");

        if (aniDb == null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "No credentials found for AniDb"
            });

        return Ok(new AniDbCredentialsResponseDto
        {
            Key = "AniDb",
            Username = aniDb.Username,
            ApiKey = aniDb.ApiKey
        });
    }

    [HttpPost]
    [Route("credentials")]
    public IActionResult Credentials([FromBody] AniDbCredentialsRequestDto requestDto)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to set credentials");

        UserPass? aniDb = CredentialManager.Credential(requestDto.Key);
        CredentialManager.SetCredentials(requestDto.Key, requestDto.Username,
            requestDto.Password ?? aniDb?.Password ?? "",
            requestDto.ApiKey);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Credentials set successfully for {0}",
            Args = [requestDto.Key]
        });
    }
}

public record PluginInfoDto
{
    [Newtonsoft.Json.JsonProperty("id")] public Guid Id { get; init; }
    [Newtonsoft.Json.JsonProperty("name")] public string Name { get; init; } = null!;
    [Newtonsoft.Json.JsonProperty("description")] public string Description { get; init; } = null!;
    [Newtonsoft.Json.JsonProperty("version")] public string Version { get; init; } = null!;
    [Newtonsoft.Json.JsonProperty("status")] public string Status { get; init; } = null!;
    [Newtonsoft.Json.JsonProperty("author")] public string? Author { get; init; }
    [Newtonsoft.Json.JsonProperty("project_url")] public string? ProjectUrl { get; init; }

    public PluginInfoDto()
    {
    }

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
