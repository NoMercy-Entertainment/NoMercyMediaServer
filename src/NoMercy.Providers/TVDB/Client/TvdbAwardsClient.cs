using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbAwardsClient: TvdbBaseClient
{
    public TvdbAwardsClient(int id = 0) : base(id)
    {
    }
    
    public Task<TvdbAwardsResponse?> Awards()
    {
        return Get<TvdbAwardsResponse>("awards");
    }
    
    public Task<TvdbAwardResponse?> Details()
    {
        return Get<TvdbAwardResponse>("awards/" + Id);
    }
    
    public Task<TvdbAwardExtendedResponse?> Extended()
    {
        return Get<TvdbAwardExtendedResponse>("awards/" + Id + "/extended");
    }
}