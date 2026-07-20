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
using NoMercy.MediaProcessing.Libraries;
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
            Folder? baseFolder = MatchLibraryFolder(albumLibrary, InputFolder);
            if (baseFolder is null)
            {
                Log.LogWarning(
                    "ReleaseImportJob: no library folder contains {InputFolder}; skipping",
                    InputFolder
                );
                return;
            }

            jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, InputFolder);
            return;
        }

        Parallel.ForEach(
            rootFolders,
            SystemParallelism.Options,
            folder =>
            {
                Log.LogInformation("Processing folder: {Path}", folder.Path);
                Folder? baseFolder = MatchLibraryFolder(albumLibrary, folder.Path);
                if (baseFolder is null)
                {
                    Log.LogWarning(
                        "ReleaseImportJob: no library folder contains {Path}; skipping",
                        folder.Path
                    );
                    return;
                }

                jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, folder.Path);
            }
        );
    }

    /// <summary>
    /// The configured library folder whose root contains <paramref name="absolutePath"/>,
    /// or null when none does. The root is resolved facade-first
    /// (<see cref="LibraryManager.ResolveScanRoot"/>): a local library's root lives in
    /// the storage facade, and the raw driver's GetFullPath canonicalizes the
    /// scope-relative folder path against the process working directory instead —
    /// so a driver-only resolve produced a bogus "/app/Libraries/Music" root, matched
    /// nothing, and the old <c>.First(...)</c> threw "Sequence contains no matching
    /// element" for every music release on a local/UNC library.
    /// </summary>
    private Folder? MatchLibraryFolder(Library library, string absolutePath) =>
        library
            .FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
            .FirstOrDefault(f =>
                absolutePath.StartsWith(
                    LibraryManager.ResolveScanRoot(
                        StorageFactory.For(f.Id, f.DriverId, string.Empty),
                        f.Path
                    ),
                    StringComparison.OrdinalIgnoreCase
                )
            );
}
