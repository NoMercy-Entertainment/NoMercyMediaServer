using NoMercy.Providers.TVDB.Models.User;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbUserClient : TvdbBaseClient
{
    public TvdbUserClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbUserResponse?> Me(bool? priority = false)
    {
        return Get<TvdbUserResponse>("user", skipCache: true, priority: priority);
    }

    public Task<TvdbUserResponse?> Details(bool? priority = false)
    {
        return Get<TvdbUserResponse>("user/" + Id, priority: priority);
    }

    public Task<TvdbUserFavoritesResponse?> Favorites(bool? priority = false)
    {
        return Get<TvdbUserFavoritesResponse>(
            "user/" + Id + "/favorites",
            skipCache: true,
            priority: priority
        );
    }
}
