using NoMercy.Providers.TMDB.Models.Certifications;
using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbTvClient : TmdbBaseClient
{
    public TmdbTvClient(int? id = 0, string[]? appendices = null, string? language = "en-US") : base((int)id!, language!)
    {
    }

    public TmdbSeasonClient Season(int seasonNumber, string[]? items = null)
    {
        return new TmdbSeasonClient(Id, seasonNumber, items);
    }

    //public Task<Models?.Season.SeasonAppends> SeasonWithAppends(int SeasonNumber, string[] Appendices)
    //{
    //	return (new SeasonClient(Id, SeasonNumber)).WithAppends(Appendices);
    //}

    public Task<TmdbTvShowDetails?> Details(bool? priority = false)
    {
        return Get<TmdbTvShowDetails>("tv/" + Id, priority: priority);
    }

    public Task<TmdbTvShowAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbTvShowAppends>("tv/" + Id, queryParams, priority);
    }

    public Task<TmdbTvShowAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "aggregate_credits",
            "alternative_titles",
            "changes",
            "content_ratings",
            "credits",
            "external_ids",
            "images",
            "keywords",
            "recommendations",
            "similar",
            "translations",
            "videos",
            "watch/providers"
        ], priority);
    }

    public Task<TmdbTvAggregatedCredits?> AggregatedCredits(bool? priority = false)
    {
        return Get<TmdbTvAggregatedCredits>("tv/" + Id + "/aggregate_credits", priority: priority);
    }

    public Task<TmdbTvAlternativeTitles?> AlternativeTitles(bool? priority = false)
    {
        return Get<TmdbTvAlternativeTitles>("tv/" + Id + "/alternative_titles", priority: priority);
    }

    public Task<TmdbTvChanges?> Changes(string startDate, string endDate, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbTvChanges>("tv/changes", queryParams, priority: priority);
    }

    public Task<TmdbTvContentRatings?> ContentRatings(bool? priority = false)
    {
        return Get<TmdbTvContentRatings>("tv/" + Id + "/content_ratings", priority: priority);
    }

    public Task<TmdbTvCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbTvCredits>("tv/" + Id + "/credits", priority: priority);
    }

    public Task<TmdbTvEpisodeGroups?> EpisodeGroups(bool? priority = false)
    {
        return Get<TmdbTvEpisodeGroups>("tv/" + Id + "/episode_groups", priority: priority);
    }

    public TmdbEpisodeGroupClient EpisodeGroup(string groupId)
    {
        return new TmdbEpisodeGroupClient(groupId);
    }

    public Task<TmdbTvExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbTvExternalIds>("tv/" + Id + "/external_ids", priority: priority);
    }

    public Task<TmdbImages?> Images(bool? priority = false)
    {
        return Get<TmdbImages>("tv/" + Id + "/images", priority: priority);
    }

    public Task<TmdbTvKeywords?> Keywords(bool? priority = false)
    {
        return Get<TmdbTvKeywords>("tv/" + Id + "/keywords", priority: priority);
    }

    public Task<TmdbTvRecommendations?> Recommendations(bool? priority = false)
    {
        return Get<TmdbTvRecommendations>("tv/" + Id + "/recommendations", priority: priority);
    }

    public Task<TmdbTvReviews?> Reviews(bool? priority = false)
    {
        return Get<TmdbTvReviews>("tv/" + Id + "/reviews", priority: priority);
    }

    public Task<TmdbTvScreenedTheatrically?> ScreenedTheatrically(bool? priority = false)
    {
        return Get<TmdbTvScreenedTheatrically>("tv/" + Id + "/screened_theatrically", priority: priority);
    }

    public Task<TmdbTvSimilar?> Similar(bool? priority = false)
    {
        return Get<TmdbTvSimilar>("tv/" + Id + "/similar", priority: priority);
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>("tv/" + Id + "/translations", priority: priority);
    }

    public Task<TmdbTvVideos?> Videos(bool? priority = false)
    {
        return Get<TmdbTvVideos>("tv/" + Id + "/videos", priority: priority);
    }

    public Task<TmdbWatchProviders?> WatchProviders(bool? priority = false)
    {
        return Get<TmdbWatchProviders>("tv/" + Id + "/watch/providers", priority: priority);
    }

    public Task<TmdbTvShowLatest?> Latest(bool? priority = false)
    {
        return Get<TmdbTvShowLatest>("tv/latest", priority: priority);
    }

    public Task<TmdbTvAiringToday?> AiringToday(bool? priority = false)
    {
        return Get<TmdbTvAiringToday>("tv/airing_today", priority: priority);
    }

    public Task<TmdbTvOnTheAir?> OnTheAir(bool? priority = false)
    {
        return Get<TmdbTvOnTheAir>("tv/on_the_air", priority: priority);
    }

    public async Task<List<TmdbTvShow>?> Popular(int limit = 10, bool? priority = false)
    {
        var response = await Get<TmdbPaginatedResponse<TmdbTvShow>>("tv/popular", priority: priority);
        return response?.Results?.Take(limit).ToList();
    }

    public Task<TmdbTvTopRated?> TopRated(bool? priority = false)
    {
        return Get<TmdbTvTopRated>("tv/top_rated", priority: priority);
    }

    public Task<TvShowCertifications?> Certifications(bool? priority = false)
    {
        return Get<TvShowCertifications>("certification/tv/list", priority: priority);
    }

    public Task<TmdbGenreTv?> Genres(string language = "en", bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["language"] = language
        };

        return Get<TmdbGenreTv>("genre/tv/list", queryParams, priority: priority);
    }
    
    public Task<TmdbTmdbNetworkDetails?> NetworkDetails(int id, bool? priority = false)
    {
        return Get<TmdbTmdbNetworkDetails>("network/" + id, priority: priority);
    }

    public Task<TmdbTmdbNetworkDetails?> CompanyDetails(int id, bool? priority = false)
    {
        return Get<TmdbTmdbNetworkDetails>("company/" + id, priority: priority);
    }
}