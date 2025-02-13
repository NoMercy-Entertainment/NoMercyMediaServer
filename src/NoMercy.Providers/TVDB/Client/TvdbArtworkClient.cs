using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbArtworkClient: TvdbBaseClient
{
    public TvdbArtworkClient(int id) : base(id)
    {
    }
    
    public Task<TvdbArtWorkResponse?> Details()
    {
        return Get<TvdbArtWorkResponse>("artwork/" + Id);
    }
    
    public Task<TvdbArtWorkExtendedResponse?> Extended()
    {
        return Get<TvdbArtWorkExtendedResponse>("artwork/" + Id + "/extended");
    }
    
    public Task<TvdbArtWorkStatusesResponse?> Statuses()
    {
        return Get<TvdbArtWorkStatusesResponse>("artwork/statuses");
    }
    
    public Task<TvdbArtWorkTypesResponse?> Types()
    {
        return Get<TvdbArtWorkTypesResponse>("artwork/types");
    }

    
}