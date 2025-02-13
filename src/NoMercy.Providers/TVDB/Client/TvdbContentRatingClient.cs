using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbContentRatingClient: TvdbBaseClient
{
    public Task<TvdbContentRatingsResponse?> ContentRatings()
    {
        return Get<TvdbContentRatingsResponse>("content/ratings");
    }
    
}