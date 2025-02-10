using NoMercy.Providers.TMDB.Models.Collections;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbCollectionClient : TmdbBaseClient
{
    public TmdbCollectionClient(int id, string[]? appendices = null) : base(id)
    {
    }

    public Task<TmdbCollectionDetails?> Details()
    {
        return Get<TmdbCollectionDetails>("collection/" + Id);
    }

    private Task<TmdbCollectionAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbCollectionAppends>("collection/" + Id, queryParams, priority);
    }

    public Task<TmdbCollectionAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "images",
            "translations"
        ], priority);
    }

    public Task<TmdbCollectionImages?> Images()
    {
        return Get<TmdbCollectionImages>("collection/" + Id + "/images");
    }

    public Task<TmdbCollectionsTranslations?> Translations()
    {
        return Get<TmdbCollectionsTranslations>("collection/" + Id + "/translations");
    }
}