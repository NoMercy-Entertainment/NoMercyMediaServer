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
[Tags("Dashboard Encoder Bundles")]
[ApiVersion(1.0)]
[Authorize(Policy = "Owner")]
[Route("api/v{version:apiVersion}/dashboard/encoder")]
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
    [HttpGet("bundle-orphans")]
    public async Task<IActionResult> BundleOrphans(CancellationToken ct)
    {
        List<Folder> folders = await folderRepository.GetAllFoldersAsync(ct);

        List<BundleOrphan> allOrphans = [];
        foreach (Folder folder in folders)
        {
            IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
            // Resolve through the driver, not the IStorage facade: the facade's
            // GetFullPath is a LocalStorage-only escape hatch that throws on every
            // remote backend, so a facade call here killed the orphan sweep for
            // NFS / SMB / S3 / WebDAV libraries.
            string libraryRoot = folderStorage.Driver.GetFullPath(folder.Path);
            IReadOnlyList<BundleOrphan> orphans = await bundleGarbageCollector.SweepAsync(
                libraryRoot,
                ct
            );
            allOrphans.AddRange(orphans);
        }

        return Ok(new { data = allOrphans });
    }
}
