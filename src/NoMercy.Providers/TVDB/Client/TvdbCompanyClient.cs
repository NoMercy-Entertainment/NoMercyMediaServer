using NoMercy.Providers.TVDB.Models.Companies;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbCompanyClient : TvdbBaseClient
{
    public TvdbCompanyClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbPaginatedResponse<TvdbCompany>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { ["page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbCompany>>("companies", query, priority);
    }

    public Task<TvdbCompanyResponse?> Details(bool? priority = false)
    {
        return Get<TvdbCompanyResponse>("companies/" + Id, priority: priority);
    }

    public Task<TvdbCompanyTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbCompanyTypesResponse>("companies/types", priority: priority);
    }
}
