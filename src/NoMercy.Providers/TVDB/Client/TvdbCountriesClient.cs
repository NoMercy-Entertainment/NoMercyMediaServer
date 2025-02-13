using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbCountriesClient: TvdbBaseClient
{
    public Task<TvdbCountriesResponse?> Countries()
    {
        return Get<TvdbCountriesResponse>("countries");
    }
    
}