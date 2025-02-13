using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbGenderClient: TvdbBaseClient
{
    public Task<TvdbGendersResponse?> Genders()
    {
        return Get<TvdbGendersResponse>("genders");
    }
    
}