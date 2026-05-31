// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class ReleaseImportJob : AbstractMusicFolderJob
{
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
            Logger.App("Processing folder: " + InputFolder, LogEventLevel.Verbose);
            Folder baseFolder = albumLibrary
                .FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
                .First(f =>
                {
                    string driverRoot = StorageFactory
                        .For(f.Id, f.DriverId, string.Empty)
                        .GetFullPath(f.Path);
                    return InputFolder.StartsWith(driverRoot, StringComparison.OrdinalIgnoreCase);
                });

            jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, InputFolder);
            return;
        }

        Parallel.ForEach(
            rootFolders,
            Config.ParallelOptions,
            folder =>
            {
                Logger.App("Processing folder: " + folder.Path);
                Folder baseFolder = albumLibrary
                    .FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
                    .First(f =>
                    {
                        string driverRoot = StorageFactory
                            .For(f.Id, f.DriverId, string.Empty)
                            .GetFullPath(f.Path);
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
