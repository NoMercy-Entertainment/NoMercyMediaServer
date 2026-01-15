// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddShowJob : AbstractMediaJob
{
    public override string QueueName => "queue";
    public override int Priority => 5;

    public bool HighPriority { get; set; }

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        ShowRepository showRepository = new(context);
        ShowManager showManager = new(showRepository, jobDispatcher);

        SeasonRepository seasonRepository = new(context);
        SeasonManager seasonManager = new(seasonRepository, jobDispatcher);

        EpisodeRepository episodeRepository = new(context);
        EpisodeManager episodeManager = new(episodeRepository, jobDispatcher);

        Library tvLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();

        TmdbTvShowAppends? show = await showManager.AddShowAsync(Id, tvLibrary, HighPriority);
        if (show == null) return;

        IEnumerable<TmdbSeasonAppends> seasons = await seasonManager.StoreSeasonsAsync(show, HighPriority);

        ConcurrentBag<Episode> episodes = [];
        await Parallel.ForEachAsync(seasons, Config.ParallelOptions, async (season, _) =>
        {
            IEnumerable<Episode> eps = await episodeManager.Add(show, season, HighPriority);
            foreach (Episode episode in eps)
            {
                episodes.Add(episode);
            } 
        });
        
        await episodeRepository.StoreEpisodes(episodes); 

        jobDispatcher.DispatchJob<RescanFilesJob>(Id, tvLibrary);
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["base","info", Id.ToString()]
        });
    }
}