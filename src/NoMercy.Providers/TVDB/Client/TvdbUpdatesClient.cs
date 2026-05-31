using NoMercy.Providers.TVDB.Models.Updates;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbUpdatesClient : TvdbBaseClient
{
    public Task<TvdbUpdatesResponse?> Updates(
        long since,
        string? type = null,
        string? action = null,
        int page = 0,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new()
        {
            ["since"] = since.ToString(),
            ["page"] = page.ToString(),
        };
        if (!string.IsNullOrEmpty(type))
            query["type"] = type;
        if (!string.IsNullOrEmpty(action))
            query["action"] = action;
        return Get<TvdbUpdatesResponse>("updates", query, priority);
    }
}
