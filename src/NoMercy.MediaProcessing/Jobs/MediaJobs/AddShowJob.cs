// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem.SystemCalls;
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

        FileRepository fileRepository = new();
        FileManager fileManager = new(fileRepository);

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
        await Parallel.ForEachAsync(seasons, async (season, _) =>
        {
            IEnumerable<Episode> eps = await episodeManager.Add(show, season, HighPriority);
            foreach (Episode episode in eps)
            {
                episodes.Add(episode);
            } 
        });
        
        await episodeRepository.StoreEpisodes(episodes); 

        await fileManager.FindFiles(Id, tvLibrary);

        Logger.App($"Show {show.Name} added to the library, extra data will be added in the background");

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["libraries", LibraryId.ToString()]
        });

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["tv", Id.ToString()]
        });
    }
}