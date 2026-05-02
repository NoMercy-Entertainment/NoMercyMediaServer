using NoMercy.Providers.TVDB.Models.Tags;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbTagsClient : TvdbBaseClient
{
    public TvdbTagsClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbTagOptionsResponse?> Options(bool? priority = false)
    {
        return Get<TvdbTagOptionsResponse>("tags/options", priority: priority);
    }

    public Task<TvdbTagOptionResponse?> Details(bool? priority = false)
    {
        return Get<TvdbTagOptionResponse>("tags/options/" + Id, priority: priority);
    }
}
