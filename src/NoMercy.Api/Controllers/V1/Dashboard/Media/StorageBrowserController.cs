using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Storage;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Nfs;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags("Dashboard Storage Browser")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/storage", Order = 10)]
public class StorageBrowserController(
    ILogger<StorageBrowserController> logger,
    IDriverRepository driverRepository,
    IStorageFactory storageFactory
) : BaseController
{
    private static readonly string[] AllowedTypes = ["local", "nfs", "s3", "r2", "webdav"];

    // Synthetic folder ID used for ad-hoc browsing through IStorageFactory.
    // The factory caches by (folderId, driverType, configHash); we want a
    // distinct slot per driver_id so different browse sessions don't share
    // a stale cache.
    private static Ulid SyntheticBrowseFolderId(Ulid driverId) => driverId;

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

        // Two probe modes:
        //   1. Enumerate exports — body has only `server`. Returns the
        //      list (which may be empty) for the Browse modal to display.
        //      Empty list is NOT a failure — TrueNAS / NFSv4-only servers
        //      legitimately don't expose an enumerable namespace.
        //   2. Test-mount — body has `server` + `export`. Actually mounts
        //      the configured export to verify connectivity. This is what
        //      the StorageModal pre-validate needs.
        bool isMountTest = !string.IsNullOrWhiteSpace(request.Config.Export);

        try
        {
            if (isMountTest)
            {
                NfsDriverConfig nfsConfig = NfsDriverConfig.For(
                    server: request.Config.Server.Trim(),
                    export: request.Config.Export!.Trim(),
                    version: request.Config.Version ?? 3,
                    uid: request.Config.Uid,
                    gid: request.Config.Gid
                );

                using NfsStorageDriver driver = new(nfsConfig);
                // Constructor throws on mount failure; reaching here = success.
                return Ok(new StorageProbeResponse { Ok = true, Exports = [] });
            }

            List<string>? exports = await NfsStorageDriver.GetExportsAsync(
                request.Config.Server.Trim(),
                logger: logger
            );

            // Empty / null is NOT an error — server may not expose a
            // browsable namespace. Return ok with an empty list and let
            // the Browse modal render a manual-entry fallback.
            return Ok(new StorageProbeResponse { Ok = true, Exports = exports ?? [] });
        }
        catch (IOException ex)
        {
            // NFS mount failure (test-mount path)
            return Ok(new StorageProbeResponse { Ok = false, Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Ok(new StorageProbeResponse { Ok = false, Error = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/storage/list
    // Universal directory browser. Takes a driver_id and resolves config
    // (including credentials) server-side via IStorageFactory — works for
    // every driver type (local, nfs, s3, r2, webdav) without the client
    // ever seeing secret material.
    // -----------------------------------------------------------------------

    [HttpPost]
    [Route("list")]
    public async Task<IActionResult> List([FromBody] StorageListRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to browse storage.");

        if (string.IsNullOrWhiteSpace(request.DriverId))
            return BadRequestResponse("driver_id is required.");

        if (!Ulid.TryParse(request.DriverId, out Ulid driverId))
            return BadRequestResponse("driver_id is not a valid ULID.");

        Driver? driver = await driverRepository.GetDriverByIdAsync(driverId);
        if (driver is null)
            return NotFoundResponse($"Driver '{request.DriverId}' not found.");

        string subPath = (request.Path ?? string.Empty).Replace('\\', '/').TrimStart('/');

        try
        {
            IStorage storage = storageFactory.For(
                folderId: SyntheticBrowseFolderId(driverId),
                driverId: driverId,
                subPath: string.Empty
            );

            IReadOnlyList<StorageEntry> entries = storage.List(
                subPath,
                pattern: null,
                recursive: false
            );

            List<StorageEntryDto> dtos =
            [
                .. entries
                    .Select(e =>
                    {
                        string name = e.Path;
                        // Storage drivers return either bare names or full
                        // paths — normalise to bare name so the client
                        // breadcrumb logic works the same regardless.
                        int slash = name.LastIndexOfAny(['/', '\\']);
                        if (slash >= 0)
                            name = name[(slash + 1)..];
                        return new StorageEntryDto { Name = name, IsDirectory = e.IsDirectory };
                    })
                    .Where(e => !string.IsNullOrEmpty(e.Name) && e.Name != "." && e.Name != "..")
                    .OrderByDescending(e => e.IsDirectory)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            ];

            return Ok(
                new StorageListResponse
                {
                    Ok = true,
                    Path = subPath,
                    Entries = dtos,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Storage list failed: driver={DriverId} type={Type} path={Path}",
                driverId,
                driver.Type,
                subPath
            );
            return Ok(new StorageListResponse { Ok = false, Error = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/storage/mkdir
    // Create a directory on the driver — used by the dashboard "create new
    // folder" flow before attaching it to a library. Idempotent: existing
    // directories return ok=true. Body: { driver_id, path }.
    // -----------------------------------------------------------------------

    [HttpPost]
    [Route("mkdir")]
    public async Task<IActionResult> Mkdir([FromBody] StorageMkdirRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create directories.");

        if (string.IsNullOrWhiteSpace(request.DriverId))
            return BadRequestResponse("driver_id is required.");

        if (!Ulid.TryParse(request.DriverId, out Ulid driverId))
            return BadRequestResponse("driver_id is not a valid ULID.");

        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequestResponse("path is required.");

        Driver? driver = await driverRepository.GetDriverByIdAsync(driverId);
        if (driver is null)
            return NotFoundResponse($"Driver '{request.DriverId}' not found.");

        string subPath = request.Path.Replace('\\', '/').TrimStart('/').TrimEnd('/');

        try
        {
            IStorage storage = storageFactory.For(
                folderId: SyntheticBrowseFolderId(driverId),
                driverId: driverId,
                subPath: string.Empty
            );

            await storage.CreateDirectoryAsync(subPath, HttpContext.RequestAborted);

            return Ok(new StorageMkdirResponse { Ok = true, Path = subPath });
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Storage mkdir failed: driver={DriverId} type={Type} path={Path}",
                driverId,
                driver.Type,
                subPath
            );
            return Ok(new StorageMkdirResponse { Ok = false, Error = ex.Message });
        }
    }
}
