using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbEpisodeGroupClient : TmdbBaseClient
{
    private readonly string _groupId;

    public TmdbEpisodeGroupClient(string groupId) : base()
    {
        _groupId = groupId;
    }

    public Task<TmdbEpisodeGroupDetails?> Details(bool? priority = false)
    {
        return Get<TmdbEpisodeGroupDetails>("tv/episode_group/" + _groupId, priority: priority);
    }
}
