using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Folders")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/folders", Order = 10)]
public class FolderBackendController(
    FolderRepository folderRepository,
    IStorageFactory storageFactory
) : BaseController
{
    private static readonly string[] AllowedBackendTypes = ["local", "smb", "nfs", "s3", "r2"];

    private static readonly string[] UnimplementedBackendTypes = [];

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/folders/backends
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("backends")]
    public IActionResult GetBackendTypes()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view backend types");

        BackendTypeMetadataDto[] metadata =
        [
            new()
            {
                Type = "local",
                DisplayName = "Local disk",
                Available = true,
                ConfigSchema = new() { { "rootPath", "string?" } },
            },
            new()
            {
                Type = "smb",
                DisplayName = "SMB/CIFS (OS mount)",
                Available = true,
                ConfigSchema = new() { { "mountPath", "string" } },
            },
            new()
            {
                Type = "nfs",
                DisplayName = "NFS (OS mount)",
                Available = true,
                ConfigSchema = new() { { "mountPath", "string" } },
            },
            new()
            {
                Type = "s3",
                DisplayName = "S3-compatible",
                Available = true,
                ConfigSchema = new()
                {
                    { "bucket", "string" },
                    { "region", "string" },
                    { "prefix", "string?" },
                    { "credentialsRef", "string?" },
                    { "endpoint", "string?" },
                },
            },
            new()
            {
                Type = "r2",
                DisplayName = "Cloudflare R2",
                Available = true,
                ConfigSchema = new()
                {
                    { "bucket", "string" },
                    { "region", "string" },
                    { "prefix", "string?" },
                    { "credentialsRef", "string?" },
                    { "endpoint", "string (required for R2)" },
                },
            },
        ];

        return Ok(metadata);
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/folders/{id}/backend
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("{id:ulid}/backend")]
    public async Task<IActionResult> GetBackend(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view folder backend config");

        Folder? folder = await folderRepository.GetFolderByIdAsync(id);
        if (folder is null)
            return NotFoundResponse("Folder not found");

        JObject? configObject = ParseConfigJson(folder.BackendConfig);

        return Ok(
            new FolderBackendDto { BackendType = folder.BackendType, BackendConfig = configObject }
        );
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/folders/{id}/backend
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route("{id:ulid}/backend")]
    public async Task<IActionResult> UpdateBackend(
        Ulid id,
        [FromBody] UpdateFolderBackendRequestDto request
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse(
                "You do not have permission to update folder backend config"
            );

        string normalizedType = (request.BackendType ?? string.Empty).Trim().ToLowerInvariant();

        if (!AllowedBackendTypes.Contains(normalizedType))
            return BadRequestResponse(
                $"Invalid backend_type '{request.BackendType}'. Allowed values: {string.Join(", ", AllowedBackendTypes)}."
            );

        string? validationError = ValidateConfig(normalizedType, request.BackendConfig);
        if (validationError is not null)
            return BadRequestResponse(validationError);

        Folder? folder = await folderRepository.GetFolderByIdAsync(id);
        if (folder is null)
            return NotFoundResponse("Folder not found");

        string? serializedConfig = request.BackendConfig is not null
            ? JsonConvert.SerializeObject(request.BackendConfig)
            : null;

        folder.BackendType = normalizedType;
        folder.BackendConfig = serializedConfig;

        await folderRepository.UpdateFolderAsync(folder);

        storageFactory.Invalidate(id);

        JObject? configObject = ParseConfigJson(folder.BackendConfig);

        string[] warnings = BuildWarnings(normalizedType);

        return Ok(
            new FolderBackendResponseDto
            {
                BackendType = folder.BackendType,
                BackendConfig = configObject,
                Warnings = warnings,
            }
        );
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static JObject? ParseConfigJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JObject.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ValidateConfig(string backendType, JObject? config)
    {
        switch (backendType)
        {
            case "local":
                if (config is null)
                    return null;

                string? rootPath = config["rootPath"]?.Value<string>();
                if (rootPath is not null && string.IsNullOrWhiteSpace(rootPath))
                    return "backend_config.rootPath must be a non-empty string when provided.";

                return null;

            case "smb":
            case "nfs":
                if (config is null)
                    return $"backend_config is required for '{backendType}' and must include 'mountPath'.";

                string? mountPath = config["mountPath"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(mountPath))
                    return $"backend_config.mountPath must be a non-empty string for '{backendType}'.";

                return null;

            case "s3":
            case "r2":
                if (config is null)
                    return $"backend_config is required for '{backendType}' and must include 'bucket' and 'region'.";

                string? bucket = config["bucket"]?.Value<string>();
                string? region = config["region"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(bucket))
                    return $"backend_config.bucket must be a non-empty string for '{backendType}'.";

                if (string.IsNullOrWhiteSpace(region))
                    return $"backend_config.region must be a non-empty string for '{backendType}'.";

                if (backendType == "r2")
                {
                    string? endpoint = config["endpoint"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(endpoint))
                        return "backend_config.endpoint is required for 'r2' (set to your R2 endpoint URL).";
                }

                return null;

            default:
                return null;
        }
    }

    private static string[] BuildWarnings(string backendType)
    {
        if (!UnimplementedBackendTypes.Contains(backendType))
            return [];

        return
        [
            $"{backendType} backend driver is not yet implemented; folder operations will fail until the driver lands.",
        ];
    }
}
