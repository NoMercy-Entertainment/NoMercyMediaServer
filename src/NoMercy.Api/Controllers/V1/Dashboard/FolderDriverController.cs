using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Folders")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/folders", Order = 10)]
public class FolderDriverController(
    FolderRepository folderRepository,
    IStorageFactory storageFactory
) : BaseController
{
    private static readonly string[] AllowedDriverTypes = ["local", "smb", "nfs", "s3", "r2"];

    private static readonly string[] UnimplementedDriverTypes = [];

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/folders/drivers
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("drivers")]
    public IActionResult GetDriverTypes()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view driver types");

        DriverMetadataDto[] metadata =
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
                DisplayName = "NFS (in-process, no OS mount required)",
                Available = true,
                ConfigSchema = new()
                {
                    { "server", "string (required) — hostname or IP of NFS server" },
                    { "export", "string (required) — export path, e.g. /exports/media" },
                    { "version", "int? (default 3) — NFS version: 3 or 4" },
                    { "uid", "int? — AUTH_UNIX user ID" },
                    { "gid", "int? — AUTH_UNIX group ID" },
                    { "port", "int? (default 2049) — NFS port" },
                    { "mountPort", "int? — mount protocol port (NFS3 only)" },
                },
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
    // GET /api/v1/dashboard/folders/{id}/driver
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("{id:ulid}/driver")]
    public async Task<IActionResult> GetDriver(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view folder driver config");

        Folder? folder = await folderRepository.GetFolderByIdAsync(id);
        if (folder is null)
            return NotFoundResponse("Folder not found");

        JObject? configObject = ParseConfigJson(folder.DriverConfig);

        return Ok(
            new FolderDriverDto { DriverType = folder.DriverType, DriverConfig = configObject }
        );
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/folders/{id}/driver
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route("{id:ulid}/driver")]
    public async Task<IActionResult> UpdateDriver(
        Ulid id,
        [FromBody] UpdateFolderDriverRequestDto request
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse(
                "You do not have permission to update folder driver config"
            );

        string normalizedType = (request.DriverType ?? string.Empty).Trim().ToLowerInvariant();

        if (!AllowedDriverTypes.Contains(normalizedType))
            return BadRequestResponse(
                $"Invalid driver_type '{request.DriverType}'. Allowed values: {string.Join(", ", AllowedDriverTypes)}."
            );

        string? validationError = ValidateConfig(normalizedType, request.DriverConfig);
        if (validationError is not null)
            return BadRequestResponse(validationError);

        Folder? folder = await folderRepository.GetFolderByIdAsync(id);
        if (folder is null)
            return NotFoundResponse("Folder not found");

        string? serializedConfig = request.DriverConfig is not null
            ? JsonConvert.SerializeObject(request.DriverConfig)
            : null;

        folder.DriverType = normalizedType;
        folder.DriverConfig = serializedConfig;

        await folderRepository.UpdateFolderAsync(folder);

        storageFactory.Invalidate(id);

        JObject? configObject = ParseConfigJson(folder.DriverConfig);

        string[] warnings = BuildWarnings(normalizedType);

        return Ok(
            new FolderDriverResponseDto
            {
                DriverType = folder.DriverType,
                DriverConfig = configObject,
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

    private static string? ValidateConfig(string driverType, JObject? config)
    {
        switch (driverType)
        {
            case "local":
                if (config is null)
                    return null;

                string? rootPath = config["rootPath"]?.Value<string>();
                if (rootPath is not null && string.IsNullOrWhiteSpace(rootPath))
                    return "driver_config.rootPath must be a non-empty string when provided.";

                return null;

            case "smb":
                if (config is null)
                    return "driver_config is required for 'smb' and must include 'mountPath'.";

                string? mountPath = config["mountPath"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(mountPath))
                    return "driver_config.mountPath must be a non-empty string for 'smb'.";

                return null;

            case "nfs":
                if (config is null)
                    return "driver_config is required for 'nfs' and must include 'server' and 'export'.";

                string? nfsServer = config["server"]?.Value<string>();
                string? nfsExport = config["export"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(nfsServer))
                    return "driver_config.server must be a non-empty string for 'nfs'.";

                if (string.IsNullOrWhiteSpace(nfsExport))
                    return "driver_config.export must be a non-empty string for 'nfs'.";

                int? nfsVersion = config["version"]?.Value<int?>();
                if (nfsVersion.HasValue && nfsVersion != 3 && nfsVersion != 4)
                    return "driver_config.version must be 3 or 4 for 'nfs'.";

                return null;

            case "s3":
            case "r2":
                if (config is null)
                    return $"driver_config is required for '{driverType}' and must include 'bucket' and 'region'.";

                string? bucket = config["bucket"]?.Value<string>();
                string? region = config["region"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(bucket))
                    return $"driver_config.bucket must be a non-empty string for '{driverType}'.";

                if (string.IsNullOrWhiteSpace(region))
                    return $"driver_config.region must be a non-empty string for '{driverType}'.";

                if (driverType == "r2")
                {
                    string? endpoint = config["endpoint"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(endpoint))
                        return "driver_config.endpoint is required for 'r2' (set to your R2 endpoint URL).";
                }

                return null;

            default:
                return null;
        }
    }

    private static string[] BuildWarnings(string driverType)
    {
        if (!UnimplementedDriverTypes.Contains(driverType))
            return [];

        return
        [
            $"{driverType} storage driver is not yet implemented; folder operations will fail until the driver lands.",
        ];
    }
}
