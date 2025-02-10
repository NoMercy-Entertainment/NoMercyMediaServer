using NoMercy.Providers.TMDB.Models.Certifications;
using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbTvClient : TmdbBaseClient
{
    public TmdbTvClient(int? id = 0, string[]? appendices = null) : base((int)id!)
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

    public Task<TmdbTvShowDetails?> Details()
    {
        return Get<TmdbTvShowDetails>("tv/" + Id);
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

    public Task<TmdbTvAggregatedCredits?> AggregatedCredits()
    {
        return Get<TmdbTvAggregatedCredits>("tv/" + Id + "/aggregate_credits");
    }

    public Task<TmdbTvAlternativeTitles?> AlternativeTitles()
    {
        return Get<TmdbTvAlternativeTitles>("tv/" + Id + "/alternative_titles");
    }

    public Task<TmdbTvChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbTvChanges>("tv/" + Id + "/changes", queryParams);
    }

    public Task<TmdbTvContentRatings?> ContentRatings()
    {
        return Get<TmdbTvContentRatings>("tv/" + Id + "/content_ratings");
    }

    public Task<TmdbTvCredits?> Credits()
    {
        return Get<TmdbTvCredits>("tv/" + Id + "/credits");
    }

    public Task<TmdbTvEpisodeGroups?> EpisodeGroups()
    {
        return Get<TmdbTvEpisodeGroups>("tv/" + Id + "/episode_groups");
    }

    public Task<TmdbTvExternalIds?> ExternalIds()
    {
        return Get<TmdbTvExternalIds>("tv/" + Id + "/external_ids");
    }

    public Task<TmdbImages?> Images()
    {
        return Get<TmdbImages>("tv/" + Id + "/images");
    }

    public Task<TmdbTvKeywords?> Keywords()
    {
        return Get<TmdbTvKeywords>("tv/" + Id + "/keywords");
    }

    public Task<TmdbTvRecommendations?> Recommendations()
    {
        return Get<TmdbTvRecommendations>("tv/" + Id + "/recommendations");
    }

    public Task<TmdbTvReviews?> Reviews()
    {
        return Get<TmdbTvReviews>("tv/" + Id + "/reviews");
    }

    public Task<TmdbTvScreenedTheatrically?> ScreenedTheatrically()
    {
        return Get<TmdbTvScreenedTheatrically>("tv/" + Id + "/screened_theatrically");
    }

    public Task<TmdbTvSimilar?> Similar()
    {
        return Get<TmdbTvSimilar>("tv/" + Id + "/similar");
    }

    public Task<TmdbSharedTranslations?> Translations()
    {
        return Get<TmdbSharedTranslations>("tv/" + Id + "/translations");
    }

    public Task<TmdbTvVideos?> Videos()
    {
        return Get<TmdbTvVideos>("tv/" + Id + "/videos");
    }

    public Task<TmdbWatchProviders?> WatchProviders()
    {
        return Get<TmdbWatchProviders>("tv/" + Id + "/watch/providers");
    }

    public Task<TmdbTvShowLatest?> Latest()
    {
        return Get<TmdbTvShowLatest>("tv/latest");
    }

    public Task<TmdbTvAiringToday?> AiringToday()
    {
        return Get<TmdbTvAiringToday>("tv/" + Id + "/airing_today");
    }

    public Task<TmdbTvOnTheAir?> OnTheAir()
    {
        return Get<TmdbTvOnTheAir>("tv/on_the_air");
    }

    public Task<List<TmdbTvShow>?> Popular(int limit = 10)
    {
        return Paginated<TmdbTvShow>("tv/popular", limit);
    }

    public Task<TmdbTvTopRated?> TopRated()
    {
        return Get<TmdbTvTopRated>("tv/top_rated");
    }

    public Task<TvShowCertifications?> Certifications()
    {
        return Get<TvShowCertifications>("certification/tv/list");
    }

    public Task<TmdbGenreTv?> Genres(string language = "en")
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["language"] = language
        };

        return Get<TmdbGenreTv>("genre/tv/list", queryParams);
    }
}
