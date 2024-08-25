// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.Networking;
using NoMercy.NmSystem;
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

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();
        
        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository, jobDispatcher);

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

        TmdbTvShowAppends? show = await showManager.AddShowAsync(Id, tvLibrary);
        if (show == null) return;

        IEnumerable<TmdbSeasonAppends> seasons = await seasonManager.StoreSeasonsAsync(show);
        foreach (TmdbSeasonAppends season in seasons)
        {
            await episodeManager.StoreEpisodes(show, season);
        }
        
        await fileManager.FindFiles(Id, tvLibrary);
        
        Logger.App($"Show {show.Name} added to the library, extra data will be added in the background");

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto()
        {
            QueryKey = ["libraries", LibraryId.ToString()]
        });

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["tv", Id.ToString()]
        });
    }
}