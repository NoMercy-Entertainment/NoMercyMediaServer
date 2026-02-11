// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class ProcessReleaseFolderJob : AbstractMusicFolderJob
{
    public override string QueueName => "queue";
    public override int Priority => 4;

    // private bool _fromFingerprint;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        Library albumLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();

        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            // .EnableFileListing()
            .Process(InputFolder, 1);

        if (rootFolders.Count == 0)
        {
            Logger.App("Processing folder: " + InputFolder, LogEventLevel.Verbose);
            Folder baseFolder = albumLibrary.FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
                .First(f => InputFolder.Contains(f.Path));
            
            jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, InputFolder);
            return;
        }
        
        Parallel.ForEach(rootFolders, Config.ParallelOptions, folder =>
        {
            Logger.App("Processing folder: " + folder.Path);
            Folder baseFolder = albumLibrary.FolderLibraries.Select(folderLibrary => folderLibrary.Folder)
                .First(f => folder.Path.Contains(f.Path));
            
            jobDispatcher.DispatchJob<AudioImportJob>(LibraryId, baseFolder.Id, folder.Path);
        });
    }
}