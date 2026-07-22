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
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Storage;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Nfs;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags(tags: "Dashboard Storage Browser")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/storage", Order = 10)]
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
    [Route(template: "probe")]
    public async Task<IActionResult> Probe([FromBody] StorageProbeRequest request)
    {

        if (string.IsNullOrWhiteSpace(value: request.Type))
            return BadRequestResponse(detail: "type is required.");

        string normalizedType = request.Type.Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(value: normalizedType))
            return BadRequestResponse(
                detail: $"Unknown type '{request.Type}'. Allowed: {string.Join(separator: ", ", value: AllowedTypes)}."
            );

        if (request.Config is null)
            return BadRequestResponse(detail: "config is required.");

        if (normalizedType != "nfs")
            return Ok(value: new StorageProbeResponse { Ok = true, Exports = [] });

        if (string.IsNullOrWhiteSpace(value: request.Config.Server))
            return BadRequestResponse(detail: "config.server is required for NFS probe.");

        // Two probe modes:
        //   1. Enumerate exports — body has only `server`. Returns the
        //      list (which may be empty) for the Browse modal to display.
        //      Empty list is NOT a failure — TrueNAS / NFSv4-only servers
        //      legitimately don't expose an enumerable namespace.
        //   2. Test-mount — body has `server` + `export`. Actually mounts
        //      the configured export to verify connectivity. This is what
        //      the StorageModal pre-validate needs.
        bool isMountTest = !string.IsNullOrWhiteSpace(value: request.Config.Export);

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

                using NfsStorageDriver driver = new(config: nfsConfig);
                // Constructor throws on mount failure; reaching here = success.
                return Ok(value: new StorageProbeResponse { Ok = true, Exports = [] });
            }

            List<string>? exports = await NfsStorageDriver.GetExportsAsync(
                server: request.Config.Server.Trim(),
                logger: logger
            );

            // Empty / null is NOT an error — server may not expose a
            // browsable namespace. Return ok with an empty list and let
            // the Browse modal render a manual-entry fallback.
            return Ok(value: new StorageProbeResponse { Ok = true, Exports = exports ?? [] });
        }
        catch (IOException ex)
        {
            // NFS mount failure (test-mount path)
            return Ok(value: new StorageProbeResponse { Ok = false, Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Ok(value: new StorageProbeResponse { Ok = false, Error = ex.Message });
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
    [Route(template: "list")]
    public async Task<IActionResult> List([FromBody] StorageListRequest request)
    {

        if (string.IsNullOrWhiteSpace(value: request.DriverId))
            return BadRequestResponse(detail: "driver_id is required.");

        if (!Ulid.TryParse(base32: request.DriverId, ulid: out Ulid driverId))
            return BadRequestResponse(detail: "driver_id is not a valid ULID.");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id: driverId);
        if (driver is null)
            return NotFoundResponse(detail: $"Driver '{request.DriverId}' not found.");

        string subPath = (request.Path ?? string.Empty).Replace(oldChar: '\\', newChar: '/').TrimStart(trimChar: '/');

        try
        {
            IStorage storage = storageFactory.For(
                folderId: SyntheticBrowseFolderId(driverId: driverId),
                driverId: driverId,
                subPath: string.Empty
            );

            IReadOnlyList<StorageEntry> entries = storage.List(
                path: subPath,
                pattern: null,
                recursive: false
            );

            List<StorageEntryDto> dtos =
            [
                .. entries
                    .Select(selector: e =>
                    {
                        string name = e.Path;
                        // Storage drivers return either bare names or full
                        // paths — normalise to bare name so the client
                        // breadcrumb logic works the same regardless.
                        int slash = name.LastIndexOfAny(anyOf: ['/', '\\']);
                        if (slash >= 0)
                            name = name[(slash + 1)..];
                        return new StorageEntryDto { Name = name, IsDirectory = e.IsDirectory };
                    })
                    .Where(predicate: e => !string.IsNullOrEmpty(value: e.Name) && e.Name != "." && e.Name != "..")
                    .OrderByDescending(keySelector: e => e.IsDirectory)
                    .ThenBy(keySelector: e => e.Name, comparer: StringComparer.OrdinalIgnoreCase),
            ];

            return Ok(
                value: new StorageListResponse
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
                exception: ex,
                message: "Storage list failed: driver={DriverId} type={Type} path={Path}", args: [driverId, driver.Type, subPath]
            );
            return Ok(value: new StorageListResponse { Ok = false, Error = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/storage/mkdir
    // Create a directory on the driver — used by the dashboard "create new
    // folder" flow before attaching it to a library. Idempotent: existing
    // directories return ok=true. Body: { driver_id, path }.
    // -----------------------------------------------------------------------

    [HttpPost]
    [Route(template: "mkdir")]
    public async Task<IActionResult> Mkdir([FromBody] StorageMkdirRequest request)
    {

        if (string.IsNullOrWhiteSpace(value: request.DriverId))
            return BadRequestResponse(detail: "driver_id is required.");

        if (!Ulid.TryParse(base32: request.DriverId, ulid: out Ulid driverId))
            return BadRequestResponse(detail: "driver_id is not a valid ULID.");

        if (string.IsNullOrWhiteSpace(value: request.Path))
            return BadRequestResponse(detail: "path is required.");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id: driverId);
        if (driver is null)
            return NotFoundResponse(detail: $"Driver '{request.DriverId}' not found.");

        string subPath = request.Path.Replace(oldChar: '\\', newChar: '/').TrimStart(trimChar: '/').TrimEnd(trimChar: '/');

        try
        {
            IStorage storage = storageFactory.For(
                folderId: SyntheticBrowseFolderId(driverId: driverId),
                driverId: driverId,
                subPath: string.Empty
            );

            await storage.CreateDirectoryAsync(path: subPath, ct: HttpContext.RequestAborted);

            return Ok(value: new StorageMkdirResponse { Ok = true, Path = subPath });
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Storage mkdir failed: driver={DriverId} type={Type} path={Path}", args: [driverId, driver.Type, subPath]
            );
            return Ok(value: new StorageMkdirResponse { Ok = false, Error = ex.Message });
        }
    }
}
