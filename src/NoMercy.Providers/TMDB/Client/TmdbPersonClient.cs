using NoMercy.Providers.TMDB.Models.People;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbPersonClient : TmdbBaseClient
{
    public TmdbPersonClient(int? id = 0, string[]? appendices = null) : base((int)id!)
    {
    }

    public Task<TmdbPersonDetails?> Details()
    {
        return Get<TmdbPersonDetails>("person/" + Id);
    }

    public Task<TmdbPersonAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbPersonAppends>("person/" + Id, queryParams, priority);
    }

    public Task<TmdbPersonAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "changes",
            "credits",
            "movie_credits",
            "combined_credits",
            "tv_credits",
            "external_ids",
            "images",
            "translations"
        ], priority);
    }

    public Task<TmdbPersonChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbPersonChanges>("person/" + Id + "/changes", queryParams);
    }

    public Task<TmdbPersonCredits?> MovieCredits()
    {
        return Get<TmdbPersonCredits>("person/" + Id + "/movie_credits");
    }

    public Task<TmdbPersonCredits?> TvCredits()
    {
        return Get<TmdbPersonCredits>("person/" + Id + "/tv_credits");
    }

    public Task<TmdbPersonExternalIds?> ExternalIds()
    {
        return Get<TmdbPersonExternalIds>("person/" + Id + "/external_ids");
    }

    public Task<TmdbPersonImages?> Images()
    {
        return Get<TmdbPersonImages>("person/" + Id + "/images");
    }

    public Task<TmdbPersonTranslations?> Translations()
    {
        return Get<TmdbPersonTranslations>("person/" + Id + "/translations");
    }

    public Task<List<TmdbPerson>?> Popular(int limit = 10)
    {
        return Paginated<TmdbPerson>("person/popular", limit);
    }
}