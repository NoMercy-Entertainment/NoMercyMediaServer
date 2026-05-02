using NoMercy.Providers.TVDB.Models.Movies;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbMovieClient : TvdbBaseClient
{
    public TvdbMovieClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbMovieResponse?> Details(bool? priority = false)
    {
        return Get<TvdbMovieResponse>("movies/" + Id, priority: priority);
    }

    public Task<TvdbMovieExtendedResponse?> Extended(
        string? meta = null,
        bool shortMeta = false,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new();
        if (!string.IsNullOrEmpty(meta))
            query["meta"] = meta;
        if (shortMeta)
            query["short"] = "true";
        return Get<TvdbMovieExtendedResponse>("movies/" + Id + "/extended", query, priority);
    }

    public Task<TvdbMovieExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended("translations", false, priority);
    }

    public Task<TvdbMovieTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbMovieTranslationResponse>(
            $"movies/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbResponse<TvdbMovie>?> BySlug(string slug, bool? priority = false)
    {
        return Get<TvdbResponse<TvdbMovie>>("movies/slug/" + slug, priority: priority);
    }

    public Task<TvdbMovieStatusesResponse?> Statuses(bool? priority = false)
    {
        return Get<TvdbMovieStatusesResponse>("movies/statuses", priority: priority);
    }

    public Task<TvdbPaginatedResponse<TvdbMovie>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { ["page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbMovie>>("movies", query, priority);
    }

    public Task<TvdbPaginatedResponse<TvdbMovie>?> Filter(
        TvdbMovieFilter filter,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = filter.ToQuery();
        return Get<TvdbPaginatedResponse<TvdbMovie>>("movies/filter", query, priority);
    }
}

public class TvdbMovieFilter
{
    public string? Country { get; set; }
    public string? Language { get; set; }
    public int? CompanyId { get; set; }
    public int? ContentRatingId { get; set; }
    public string? GenreIds { get; set; }
    public int? SortBy { get; set; }
    public string? SortType { get; set; }
    public int? Status { get; set; }
    public int? Year { get; set; }
    public int Page { get; set; }

    internal Dictionary<string, string?> ToQuery()
    {
        Dictionary<string, string?> q = new();
        if (Country is not null)
            q["country"] = Country;
        if (Language is not null)
            q["lang"] = Language;
        if (CompanyId is not null)
            q["company"] = CompanyId.Value.ToString();
        if (ContentRatingId is not null)
            q["contentRating"] = ContentRatingId.Value.ToString();
        if (!string.IsNullOrEmpty(GenreIds))
            q["genre"] = GenreIds;
        if (SortBy is not null)
            q["sort"] = SortBy.Value.ToString();
        if (!string.IsNullOrEmpty(SortType))
            q["sortType"] = SortType;
        if (Status is not null)
            q["status"] = Status.Value.ToString();
        if (Year is not null)
            q["year"] = Year.Value.ToString();
        q["page"] = Page.ToString();
        return q;
    }
}
