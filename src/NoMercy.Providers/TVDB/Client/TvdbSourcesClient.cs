using NoMercy.Providers.TVDB.Models.Sources;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbSourcesClient : TvdbBaseClient
{
    public Task<TvdbSourceTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbSourceTypesResponse>("sources/types", priority: priority);
    }
}
