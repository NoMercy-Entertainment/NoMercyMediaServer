using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbGenresClient: TvdbBaseClient
{
    public TvdbGenresClient(int id) : base(id)
    {
    }
    
    public Task<TvdbGenresResponse?> Genres()
    {
        return Get<TvdbGenresResponse>("genres");
    }
    
    public Task<TvdbGenreResponse?> Details()
    {
        return Get<TvdbGenreResponse>("genres/" + Id);
    }
    
}