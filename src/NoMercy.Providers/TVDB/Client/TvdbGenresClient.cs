using NoMercy.Providers.TVDB.Models.Genres;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbGenresClient : TvdbBaseClient
{
    public TvdbGenresClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbGenresResponse?> Genres(bool? priority = false)
    {
        return Get<TvdbGenresResponse>("genres", priority: priority);
    }

    public Task<TvdbGenreResponse?> Details(bool? priority = false)
    {
        return Get<TvdbGenreResponse>("genres/" + Id, priority: priority);
    }
}
