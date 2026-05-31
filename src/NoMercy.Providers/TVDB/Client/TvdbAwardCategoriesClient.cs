using NoMercy.Providers.TVDB.Models.Awards;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbAwardCategoriesClient : TvdbBaseClient
{
    public TvdbAwardCategoriesClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbAwardCategoryResponse?> Details(bool? priority = false)
    {
        return Get<TvdbAwardCategoryResponse>("awards/categories/" + Id, priority: priority);
    }

    public Task<TvdbAwardCategoryExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbAwardCategoryExtendedResponse>(
            "awards/categories/" + Id + "/extended",
            priority: priority
        );
    }
}
