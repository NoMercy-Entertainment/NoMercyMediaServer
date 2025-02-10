using NoMercy.Providers.TMDB.Client.Mocks;
using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using TmdbMovieCertifications = NoMercy.Providers.TMDB.Models.Certifications.TmdbMovieCertifications;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbMovieClient : TmdbBaseClient, ITmdbMovieClient
{
    private readonly MovieResponseMocks? _mockDataProvider;

    public TmdbMovieClient(int? id = 0, string[]? appendices = null, MovieResponseMocks? mockDataProvider = null) :
        base((int)id!)
    {
        _mockDataProvider = mockDataProvider;
    }

    public Task<TmdbMovieDetails?> Details()
    {
        return Get<TmdbMovieDetails>("movie/" + Id);
    }

    private Task<TmdbMovieAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["append_to_response"] = string.Join(",", appendices)
        };

        return Get<TmdbMovieAppends>("movie/" + Id, queryParams, priority);
    }

    public Task<TmdbMovieAppends?> WithAllAppends(bool? priority = false)
    {
        if (_mockDataProvider != null)
        {
            return Task.FromResult(_mockDataProvider.MockMovieAppendsResponse());
        }

        return WithAppends([
            "alternative_titles",
            "release_dates",
            "changes",
            "credits",
            "keywords",
            "recommendations",
            "similar",
            "translations",
            "external_ids",
            "videos",
            "images",
            "watch/providers"
        ], priority);
    }

    public Task<TmdbMovieAggregatedCredits?> AggregatedCredits()
    {
        return Get<TmdbMovieAggregatedCredits>("movie/" + Id + "/aggregate_credits");
    }

    public Task<TmdbMovieAlternativeTitles?> AlternativeTitles()
    {
        return Get<TmdbMovieAlternativeTitles>("movie/" + Id + "/alternative_titles");
    }

    public Task<TmdbMovieChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Get<TmdbMovieChanges>("movie/" + Id + "/changes", queryParams);
    }

    public Task<TmdbMovieCredits?> Credits()
    {
        return Get<TmdbMovieCredits>("movie/" + Id + "/credits");
    }

    public Task<TmdbMovieExternalIds?> ExternalIds()
    {
        return Get<TmdbMovieExternalIds>("movie/" + Id + "/external_ids");
    }

    public Task<TmdbImages?> Images()
    {
        return Get<TmdbImages>("movie/" + Id + "/images");
    }

    public Task<TmdbMovieKeywords?> Keywords()
    {
        return Get<TmdbMovieKeywords>("movie/" + Id + "/keywords");
    }

    public Task<TmdbMovieLists?> Lists()
    {
        return Get<TmdbMovieLists>("movie/" + Id + "/lists");
    }

    public Task<TmdbMovieRecommendations?> Recommendations()
    {
        return Get<TmdbMovieRecommendations>("movie/" + Id + "/recommendations");
    }

    public Task<TmdbMovieReleaseDates?> ReleaseDates()
    {
        return Get<TmdbMovieReleaseDates>("movie/" + Id + "/release_dates");
    }

    public Task<TmdbMovieReviews?> Reviews()
    {
        return Get<TmdbMovieReviews>("movie/" + Id + "/reviews");
    }

    public Task<TmdbMovieSimilar?> Similar()
    {
        return Get<TmdbMovieSimilar>("movie/" + Id + "/similar");
    }

    public Task<TmdbSharedTranslations?> Translations()
    {
        return Get<TmdbSharedTranslations>("movie/" + Id + "/translations");
    }

    public Task<TmdbMovieVideos?> Videos()
    {
        return Get<TmdbMovieVideos>("movie/" + Id + "/videos");
    }

    public Task<TmdbMovieWatchProviders?> WatchProviders()
    {
        return Get<TmdbMovieWatchProviders>("movie/" + Id + "/watch/providers");
    }

    public Task<TmdbMovieLatest?> Latest()
    {
        return Get<TmdbMovieLatest>("movie/" + Id + "/latest");
    }

    public Task<TmdbMovieNowPlaying?> NowPlaying()
    {
        return Get<TmdbMovieNowPlaying>("movie/" + Id + "/now_playing");
    }

    public Task<List<TmdbMovie>?> Popular(int limit = 10)
    {
        return Paginated<TmdbMovie>("movie/popular", limit);
    }

    public Task<TmdbMovieTopRated?> TopRated()
    {
        return Get<TmdbMovieTopRated>("movie/" + Id + "/top_rated");
    }

    public Task<TmdbMovieUpcoming?> Upcoming()
    {
        return Get<TmdbMovieUpcoming>("movie/" + Id + "/upcoming");
    }

    public Task<TmdbMovieCertifications?> Certifications()
    {
        return Get<TmdbMovieCertifications>("certification/movie/list");
    }

    public Task<TmdbGenreMovies?> Genres(string language = "en")
    {
        return Get<TmdbGenreMovies>("genre/movie/list", new Dictionary<string, string?>
        {
            ["language"] = language
        });
    }
}
