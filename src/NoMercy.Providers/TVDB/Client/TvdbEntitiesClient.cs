using NoMercy.Providers.TVDB.Models.Entities;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbEntitiesClient : TvdbBaseClient
{
    public Task<TvdbEntitiesResponse?> Entities(bool? priority = false)
    {
        return Get<TvdbEntitiesResponse>("entities/types", priority: priority);
    }
}
