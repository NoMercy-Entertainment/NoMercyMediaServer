using NoMercy.Providers.TVDB.Models.Seasons;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbSeasonClient : TvdbBaseClient
{
    public TvdbSeasonClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbSeasonResponse?> Details(bool? priority = false)
    {
        return Get<TvdbSeasonResponse>("seasons/" + Id, priority: priority);
    }

    public Task<TvdbSeasonExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbSeasonExtendedResponse>("seasons/" + Id + "/extended", priority: priority);
    }

    public Task<TvdbSeasonExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended(priority);
    }

    public Task<TvdbSeasonTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbSeasonTranslationResponse>(
            $"seasons/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbSeasonTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbSeasonTypesResponse>("seasons/types", priority: priority);
    }
}
