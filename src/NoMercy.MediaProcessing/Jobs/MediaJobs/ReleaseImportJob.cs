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

// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.AcoustId;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class ReleaseImportJob : AbstractMusicFolderJob
{
    public ReleaseImportJob() { }

    public ReleaseImportJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        IAudioFingerprinter audioFingerprinter,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory, storageDriver, audioFingerprinter, loggerFactory) { }

    public override string QueueName => "import";
    public override int Priority => 4;

    // private bool _fromFingerprint;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        Library albumLibrary = await context
            .Libraries.Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
                .ThenInclude(f => f.Folder)
            .FirstAsync();

        await using MediaScan mediaScan = new(StorageDriver);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            // .EnableFileListing()
            .Process(InputFolder, 1);

        if (rootFolders.Count == 0)
        {
            Log.LogTrace("Processing folder: {InputFolder}", InputFolder);
            Folder baseFolder = albumLibrary
                .FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
                .First(f =>
                {
                    // Resolve through the driver, not the IStorage facade: the
                    // facade's GetFullPath is a LocalStorage-only escape hatch
                    // that throws on every remote backend, so a facade call
                    // here killed folder matching for NFS / SMB / S3 / WebDAV
                    // music libraries.
                    string driverRoot = StorageFactory
                        .For(f.Id, f.DriverId, string.Empty)
                        .Driver.GetFullPath(f.Path);
                    return InputFolder.StartsWith(driverRoot, StringComparison.OrdinalIgnoreCase);
                });

            jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, InputFolder);
            return;
        }

        Parallel.ForEach(
            rootFolders,
            SystemParallelism.Options,
            folder =>
            {
                Log.LogInformation("Processing folder: {Path}", folder.Path);
                Folder baseFolder = albumLibrary
                    .FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
                    .First(f =>
                    {
                        // Resolve through the driver, not the IStorage facade: the
                        // facade's GetFullPath is a LocalStorage-only escape hatch
                        // that throws on every remote backend, so a facade call
                        // here killed folder matching for NFS / SMB / S3 / WebDAV
                        // music libraries.
                        string driverRoot = StorageFactory
                            .For(f.Id, f.DriverId, string.Empty)
                            .Driver.GetFullPath(f.Path);
                        return folder.Path.StartsWith(
                            driverRoot,
                            StringComparison.OrdinalIgnoreCase
                        );
                    });

                jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, folder.Path);
            }
        );
    }
}
