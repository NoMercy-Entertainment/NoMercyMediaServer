using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Seasons;

public interface ISeasonManager
{
    Task<List<TmdbSeasonAppends>> StoreSeasonsAsync(TmdbTvShowAppends show);
    Task UpdateSeasonAsync(string showName, TmdbSeasonAppends season);
    Task RemoveSeasonAsync(string showName, TmdbSeasonAppends season);
}