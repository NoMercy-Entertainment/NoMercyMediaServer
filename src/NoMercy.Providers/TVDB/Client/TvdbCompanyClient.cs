using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbCompanyClient(int id) : TvdbBaseClient(id)
{
    public Task<TvdbCompaniesResponse?> Companies()
    {
        return Get<TvdbCompaniesResponse>("companies");
    }
    
    public Task<TvdbCompanyResponse?> Details()
    {
        return Get<TvdbCompanyResponse>("companies/" + Id);
    }
    
    public Task<TvdbCompanyTypesResponse?> Types()
    {
        return Get<TvdbCompanyTypesResponse>("companies/types");
    }
}