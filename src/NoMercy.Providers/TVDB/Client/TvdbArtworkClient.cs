using NoMercy.Providers.TVDB.Models.Artwork;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbArtworkClient : TvdbBaseClient
{
    public TvdbArtworkClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbArtworkResponse?> Details(bool? priority = false)
    {
        return Get<TvdbArtworkResponse>("artwork/" + Id, priority: priority);
    }

    public Task<TvdbArtworkExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbArtworkExtendedResponse>("artwork/" + Id + "/extended", priority: priority);
    }

    public Task<TvdbArtworkStatusesResponse?> Statuses(bool? priority = false)
    {
        return Get<TvdbArtworkStatusesResponse>("artwork/statuses", priority: priority);
    }

    public Task<TvdbArtworkTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbArtworkTypesResponse>("artwork/types", priority: priority);
    }
}
