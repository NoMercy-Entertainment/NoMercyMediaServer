// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

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
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddMovieJob : AbstractMediaJob
{
    public override string QueueName => "queue";
    public override int Priority => 5;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository);

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(movieRepository, jobDispatcher);

        Library movieLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();

        TmdbMovieAppends? movieAppends = await movieManager.Add(Id, movieLibrary);
        if (movieAppends == null) return;

        if (movieAppends.BelongsToCollection != null)
            jobDispatcher.DispatchJob<AddCollectionJob>(movieAppends.BelongsToCollection.Id, LibraryId);
        
        jobDispatcher.DispatchJob<RescanFilesJob>(Id, movieLibrary);

        Logger.App($"Movie {Id} added to library, extra data will be added in the background");
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["base","info", Id.ToString()]
        });
    }
}