using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbPersonClient(int id) : TvdbBaseClient(id)
{
    public Task<TvdbCharacterResponse?> Character()
    {
        return Get<TvdbCharacterResponse>("characters/" + Id);
    }
    
    // public Task<TvdbPeopleResponse?> People()
    // {
    //     return Get<TvdbPeopleResponse>("people");
    // }
    
    // public Task<TvdbPersonResponse?> Details()
    // {
    //     return Get<TvdbPersonResponse>("people/" + Id);
    // }
    //
    // public Task<TvdbPersonExtendedResponse?> Extended()
    // {
    //     return Get<TvdbPersonExtendedResponse>("people/" + Id + "/extended");
    // }
    
    public Task<TvdbPeopleTypeResponse?> Types()
    {
        return Get<TvdbPeopleTypeResponse>("people/types");
    }
}