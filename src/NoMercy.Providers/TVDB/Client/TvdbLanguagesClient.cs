using NoMercy.Providers.TVDB.Models.Languages;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbLanguagesClient : TvdbBaseClient
{
    public Task<TvdbLanguagesResponse?> Languages(bool? priority = false)
    {
        return Get<TvdbLanguagesResponse>("languages", priority: priority);
    }
}
