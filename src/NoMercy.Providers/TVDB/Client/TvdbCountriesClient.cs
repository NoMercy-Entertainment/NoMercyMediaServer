using NoMercy.Providers.TVDB.Models.Countries;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbCountriesClient : TvdbBaseClient
{
    public Task<TvdbCountriesResponse?> Countries(bool? priority = false)
    {
        return Get<TvdbCountriesResponse>("countries", priority: priority);
    }
}
