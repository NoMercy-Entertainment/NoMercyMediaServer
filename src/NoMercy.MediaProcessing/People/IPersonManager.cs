using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.People;

public interface IPersonManager
{
    public Task Store(TmdbTvShowAppends show);
    public Task Update(string showName, TmdbTvShowAppends show);
    public Task Remove(string showName, TmdbTvShowAppends show);
}