using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage.Drivers.Nfs;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Storage Browser")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/storage", Order = 10)]
public class StorageBrowserController(ILogger<StorageBrowserController> logger) : BaseController
{
    private static readonly string[] AllowedTypes = ["local", "nfs", "s3", "r2", "webdav"];

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/storage/probe
    // Enumerate exports / connectivity check for a storage config.
    // -----------------------------------------------------------------------

    [HttpPost]
    [Route("probe")]
    public async Task<IActionResult> Probe([FromBody] StorageProbeRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to probe storage.");

        if (string.IsNullOrWhiteSpace(request.Type))
            return BadRequestResponse("type is required.");

        string normalizedType = request.Type.Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(normalizedType))
            return BadRequestResponse(
                $"Unknown type '{request.Type}'. Allowed: {string.Join(", ", AllowedTypes)}."
            );

        if (request.Config is null)
            return BadRequestResponse("config is required.");

        if (normalizedType != "nfs")
            return Ok(new StorageProbeResponse { Ok = true, Exports = [] });

        if (string.IsNullOrWhiteSpace(request.Config.Server))
            return BadRequestResponse("config.server is required for NFS probe.");

        try
        {
            List<string>? exports = await NfsStorageDriver.GetExportsAsync(
                request.Config.Server.Trim(),
                logger: logger
            );

            if (exports is null)
                return Ok(
                    new StorageProbeResponse
                    {
                        Ok = false,
                        Error =
                            $"No exports returned — server '{request.Config.Server}' may be unreachable or have no configured exports.",
                    }
                );

            return Ok(new StorageProbeResponse { Ok = true, Exports = exports });
        }
        catch (Exception ex)
        {
            return Ok(new StorageProbeResponse { Ok = false, Error = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/storage/list
    // List directory contents at a sub-path within a storage export.
    // -----------------------------------------------------------------------

    [HttpPost]
    [Route("list")]
    public IActionResult List([FromBody] StorageListRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to browse storage.");

        if (string.IsNullOrWhiteSpace(request.Type))
            return BadRequestResponse("type is required.");

        string normalizedType = request.Type.Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(normalizedType))
            return BadRequestResponse(
                $"Unknown type '{request.Type}'. Allowed: {string.Join(", ", AllowedTypes)}."
            );

        if (request.Config is null)
            return BadRequestResponse("config is required.");

        if (normalizedType != "nfs")
            return Ok(
                new StorageListResponse
                {
                    Ok = false,
                    Error = $"Browser not supported for storage type '{normalizedType}' yet.",
                }
            );

        if (string.IsNullOrWhiteSpace(request.Config.Server))
            return BadRequestResponse("config.server is required for NFS list.");

        if (string.IsNullOrWhiteSpace(request.Config.Export))
            return BadRequestResponse("config.export is required for NFS list.");

        NfsDriverConfig config = NfsDriverConfig.For(
            server: request.Config.Server.Trim(),
            export: request.Config.Export.Trim(),
            version: request.Config.Version ?? 3,
            uid: request.Config.Uid,
            gid: request.Config.Gid
        );

        string relativePath = request.Path ?? string.Empty;

        NfsStorageDriver? driver = null;
        try
        {
            driver = new NfsStorageDriver(config);
            List<(string Name, bool IsDirectory)> entries = driver.ListDirectories(relativePath);

            List<StorageEntryDto> dtos = entries
                .Select(e => new StorageEntryDto { Name = e.Name, IsDirectory = e.IsDirectory })
                .OrderBy(e => e.Name)
                .ToList();

            return Ok(
                new StorageListResponse
                {
                    Ok = true,
                    Path = relativePath,
                    Entries = dtos,
                }
            );
        }
        catch (Exception ex)
        {
            return Ok(new StorageListResponse { Ok = false, Error = ex.Message });
        }
        finally
        {
            driver?.Dispose();
        }
    }
}
