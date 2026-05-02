using NoMercy.Providers.TVDB.Models.ContentRatings;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbContentRatingClient : TvdbBaseClient
{
    public Task<TvdbContentRatingsResponse?> ContentRatings(bool? priority = false)
    {
        return Get<TvdbContentRatingsResponse>("content/ratings", priority: priority);
    }
}
