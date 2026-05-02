using NoMercy.Providers.TVDB.Models.Genders;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbGenderClient : TvdbBaseClient
{
    public Task<TvdbGendersResponse?> Genders(bool? priority = false)
    {
        return Get<TvdbGendersResponse>("genders", priority: priority);
    }
}
