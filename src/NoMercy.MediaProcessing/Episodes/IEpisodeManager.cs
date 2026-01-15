using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Episodes;

public interface IEpisodeManager
{
    public Task<IEnumerable<Episode>> Add(TmdbTvShow show, TmdbSeasonAppends season, bool? priority = false);
}