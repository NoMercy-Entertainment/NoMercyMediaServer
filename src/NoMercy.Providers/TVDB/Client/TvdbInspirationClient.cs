using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbInspirationClient: TvdbBaseClient
{
    public Task<TvdbInspirationTypesResponse?> InspirationTypes()
    {
        return Get<TvdbInspirationTypesResponse>("inspiration/types");
    }
    
}