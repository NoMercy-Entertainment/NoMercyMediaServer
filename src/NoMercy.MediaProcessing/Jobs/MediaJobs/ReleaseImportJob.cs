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
        : base(storageFactory: storageFactory, storageDriver: storageDriver, audioFingerprinter: audioFingerprinter, loggerFactory: loggerFactory) { }

    public override string QueueName => "import";
    public override int Priority => 4;

    // private bool _fromFingerprint;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        Library albumLibrary = await context
            .Libraries.Where(predicate: f => f.Id == LibraryId)
            .Include(navigationPropertyPath: f => f.FolderLibraries)
                .ThenInclude(navigationPropertyPath: f => f.Folder)
            .FirstAsync();

        await using MediaScan mediaScan = new(driver: StorageDriver);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            // .EnableFileListing()
            .Process(rootFolder: InputFolder, depth: 1);

        if (rootFolders.Count == 0)
        {
            Log.LogTrace(message: "Processing folder: {InputFolder}", args: InputFolder);
            Folder? baseFolder = MatchLibraryFolder(library: albumLibrary, absolutePath: InputFolder);
            if (baseFolder is null)
            {
                Log.LogWarning(
                    message: "ReleaseImportJob: no library folder contains {InputFolder}; skipping",
                    args: InputFolder
                );
                return;
            }

            jobDispatcher.DispatchJob<AudioImportJob>(libraryId: LibraryId, folderId: baseFolder.Id, filePath: InputFolder);
            return;
        }

        Parallel.ForEach(
            source: rootFolders,
            parallelOptions: SystemParallelism.Options,
            body: folder =>
            {
                Log.LogInformation(message: "Processing folder: {Path}", args: folder.Path);
                Folder? baseFolder = MatchLibraryFolder(library: albumLibrary, absolutePath: folder.Path);
                if (baseFolder is null)
                {
                    Log.LogWarning(
                        message: "ReleaseImportJob: no library folder contains {Path}; skipping",
                        args: folder.Path
                    );
                    return;
                }

                jobDispatcher.DispatchJob<AudioImportJob>(libraryId: LibraryId, folderId: baseFolder.Id, filePath: folder.Path);
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
            .FolderLibraries.Select(selector: folderLibrary => folderLibrary.Folder)
            .FirstOrDefault(predicate: f =>
                absolutePath.StartsWith(
                    value: LibraryManager.ResolveScanRoot(
                        storage: StorageFactory.For(folderId: f.Id, driverId: f.DriverId, subPath: string.Empty),
                        folderPath: f.Path
                    ),
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            );
}
