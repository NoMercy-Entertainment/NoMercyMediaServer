using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbLanguagesClient: TvdbBaseClient
{
    public Task<TvdbLanguagesResponse?> Languages()
    {
        return Get<TvdbLanguagesResponse>("languages");
    }
    
}