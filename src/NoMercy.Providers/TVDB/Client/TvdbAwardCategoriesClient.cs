using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbAwardCategoriesClient: TvdbBaseClient
{
    public TvdbAwardCategoriesClient(int id = 0) : base(id)
    {
    }
    
    public Task<TvdbAwardExtendedResponse?> Extended()
    {
        return Get<TvdbAwardExtendedResponse>("awards/" + Id + "/extended");
    }
    
    public Task<TvdbAwardCategoryResponse?> Categories()
    {
        return Get<TvdbAwardCategoryResponse>("awards/categories/" + Id);
    }
    
    public Task<TvdbAwardCategoryExtendedResponse?> CategoriesExtended()
    {
        return Get<TvdbAwardCategoryExtendedResponse>("awards/categories/" + Id + "/extended");
    }

    
}