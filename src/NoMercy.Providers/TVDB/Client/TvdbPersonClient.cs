using NoMercy.Providers.TVDB.Models.Characters;
using NoMercy.Providers.TVDB.Models.People;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbPersonClient : TvdbBaseClient
{
    public TvdbPersonClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbPersonResponse?> Details(bool? priority = false)
    {
        return Get<TvdbPersonResponse>("people/" + Id, priority: priority);
    }

    public Task<TvdbPersonExtendedResponse?> Extended(string? meta = null, bool? priority = false)
    {
        Dictionary<string, string?> query = new();
        if (!string.IsNullOrEmpty(meta))
            query["meta"] = meta;
        return Get<TvdbPersonExtendedResponse>("people/" + Id + "/extended", query, priority);
    }

    public Task<TvdbPersonExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended("translations", priority);
    }

    public Task<TvdbPersonTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbPersonTranslationResponse>(
            $"people/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbPersonTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbPersonTypesResponse>("people/types", priority: priority);
    }

    public Task<TvdbCharacterResponse?> Character(bool? priority = false)
    {
        return Get<TvdbCharacterResponse>("characters/" + Id, priority: priority);
    }
}
