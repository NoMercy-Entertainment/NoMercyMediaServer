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
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.Bundle;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard.Encoder;

[ApiController]
[Tags(tags: "Dashboard Encoder Bundles")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Owner")]
[Route(template: "api/v{version:apiVersion}/dashboard/encoder")]
public class EncoderBundleController(
    IBundleGarbageCollector bundleGarbageCollector,
    IFolderRepository folderRepository,
    IStorageFactory storageFactory
) : BaseController
{
    /// <summary>
    /// Sweeps all configured library folders for orphan encode bundles —
    /// bundles whose preset has been deleted, that contain structural
    /// anomalies (duplicate manifests), or whose file inventory mismatches
    /// the on-disk state. Results are surfaced for the user to review and
    /// purge; no files are deleted by this endpoint.
    /// </summary>
    [HttpGet(template: "bundle-orphans")]
    public async Task<IActionResult> BundleOrphans(CancellationToken ct)
    {
        List<Folder> folders = await folderRepository.GetAllFoldersAsync(ct: ct);

        List<BundleOrphan> allOrphans = [];
        foreach (Folder folder in folders)
        {
            IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
            // Resolve through the driver, not the IStorage facade: the facade's
            // GetFullPath is a LocalStorage-only escape hatch that throws on every
            // remote backend, so a facade call here killed the orphan sweep for
            // NFS / SMB / S3 / WebDAV libraries.
            string libraryRoot = folderStorage.Driver.GetFullPath(path: folder.Path);
            IReadOnlyList<BundleOrphan> orphans = await bundleGarbageCollector.SweepAsync(
                libraryRoot: libraryRoot,
                ct: ct
            );
            allOrphans.AddRange(collection: orphans);
        }

        return Ok(value: new { data = allOrphans });
    }
}
