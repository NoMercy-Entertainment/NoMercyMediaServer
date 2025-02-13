using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbEntitiesClient: TvdbBaseClient
{
    public Task<TvdbEntitiesResponse?> Entities()
    {
        return Get<TvdbEntitiesResponse>("entities");
    }
    
}