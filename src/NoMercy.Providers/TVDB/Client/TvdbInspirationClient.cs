using NoMercy.Providers.TVDB.Models.Inspirations;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbInspirationClient : TvdbBaseClient
{
    public Task<TvdbInspirationTypesResponse?> InspirationTypes(bool? priority = false)
    {
        return Get<TvdbInspirationTypesResponse>("inspiration/types", priority: priority);
    }
}
