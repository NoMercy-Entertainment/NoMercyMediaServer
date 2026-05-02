using NoMercy.Providers.TVDB.Models.Artwork;
using NoMercy.Providers.TVDB.Models.Series;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbSeriesClient : TvdbBaseClient
{
    public TvdbSeriesClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbSeriesResponse?> Details(bool? priority = false)
    {
        return Get<TvdbSeriesResponse>("series/" + Id, priority: priority);
    }

    public Task<TvdbSeriesExtendedResponse?> Extended(
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
        return Get<TvdbSeriesExtendedResponse>("series/" + Id + "/extended", query, priority);
    }

    public Task<TvdbSeriesExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended("translations,episodes", false, priority);
    }

    public Task<TvdbSeriesEpisodesResponse?> Episodes(
        string seasonType = "default",
        int page = 0,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new() { ["page"] = page.ToString() };
        return Get<TvdbSeriesEpisodesResponse>(
            $"series/{Id}/episodes/{seasonType}",
            query,
            priority
        );
    }

    public Task<TvdbSeriesEpisodesResponse?> EpisodesTranslated(
        string seasonType,
        string language,
        int page = 0,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new() { ["page"] = page.ToString() };
        return Get<TvdbSeriesEpisodesResponse>(
            $"series/{Id}/episodes/{seasonType}/{language}",
            query,
            priority
        );
    }

    public Task<TvdbSeriesTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbSeriesTranslationResponse>(
            $"series/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbResponse<TvdbArtwork[]>?> Artworks(
        int? type = null,
        string? language = null,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new();
        if (type is not null)
            query["type"] = type.Value.ToString();
        if (!string.IsNullOrEmpty(language))
            query["lang"] = language;
        return Get<TvdbResponse<TvdbArtwork[]>>("series/" + Id + "/artworks", query, priority);
    }

    public Task<TvdbNextAiredResponse?> NextAired(bool? priority = false)
    {
        return Get<TvdbNextAiredResponse>("series/" + Id + "/nextAired", priority: priority);
    }

    public Task<TvdbResponse<TvdbSeries>?> BySlug(string slug, bool? priority = false)
    {
        return Get<TvdbResponse<TvdbSeries>>("series/slug/" + slug, priority: priority);
    }

    public Task<TvdbSeriesStatusesResponse?> Statuses(bool? priority = false)
    {
        return Get<TvdbSeriesStatusesResponse>("series/statuses", priority: priority);
    }

    public Task<TvdbPaginatedResponse<TvdbSeries>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { ["page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbSeries>>("series", query, priority);
    }

    public Task<TvdbPaginatedResponse<TvdbSeries>?> Filter(
        TvdbSeriesFilter filter,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = filter.ToQuery();
        return Get<TvdbPaginatedResponse<TvdbSeries>>("series/filter", query, priority);
    }
}

public class TvdbSeriesFilter
{
    public string? Country { get; set; }
    public string? Language { get; set; }
    public int? CompanyId { get; set; }
    public int? ContentRatingId { get; set; }
    public string? GenreIds { get; set; }
    public int? Lang { get; set; }
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
