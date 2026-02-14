// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
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

        bool wasEmpty = !await context.LibraryTv.AnyAsync(lt => lt.LibraryId == LibraryId);

        TmdbTvShowAppends? show = await showManager.AddShowAsync(Id, tvLibrary, HighPriority);
        if (show == null) return;

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(new MediaAddedEvent
            {
                MediaId = Id,
                MediaType = "tvshow",
                Title = show.Name ?? $"Show {Id}",
                LibraryId = LibraryId
            });
        }

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
        
        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["base", "info", Id.ToString()]
            });

            if (wasEmpty)
                await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                {
                    QueryKey = ["libraries"]
                });
        }
    }
}