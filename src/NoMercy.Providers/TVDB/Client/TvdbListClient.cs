using NoMercy.Providers.TVDB.Models.Lists;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbListClient : TvdbBaseClient
{
    public TvdbListClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbPaginatedResponse<TvdbList>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { ["page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbList>>("lists", query, priority);
    }

    public Task<TvdbListResponse?> Details(bool? priority = false)
    {
        return Get<TvdbListResponse>("lists/" + Id, priority: priority);
    }

    public Task<TvdbListExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbListExtendedResponse>("lists/" + Id + "/extended", priority: priority);
    }

    public Task<TvdbListResponse?> BySlug(string slug, bool? priority = false)
    {
        return Get<TvdbListResponse>("lists/slug/" + slug, priority: priority);
    }

    public Task<TvdbListTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbListTranslationResponse>(
            $"lists/{Id}/translations/{language}",
            priority: priority
        );
    }
}
