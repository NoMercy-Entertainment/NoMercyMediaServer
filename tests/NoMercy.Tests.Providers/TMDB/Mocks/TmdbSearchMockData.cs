using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Mocks;

/// <summary>
/// Mock data for TMDB search operations testing
/// Provides realistic test data for all search types
/// </summary>
public static class TmdbSearchMockData
{
    /// <summary>
    /// Creates a sample movie search response for testing
    /// </summary>
    public static TmdbPaginatedResponse<TmdbMovie> GetSampleMovieSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 2,
            TotalPages = 1,
            Results =
            [
                new()
                {
                    Id = 27205,
                    Title = "Inception",
                    OriginalTitle = "Inception",
                    Overview = "A thief who steals corporate secrets through the use of dream-sharing technology...",
                    ReleaseDate = DateTime.Parse("2010-07-16"),
                    PosterPath = "/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg",
                    BackdropPath = "/s3TBrRGB1iav7gFOCNx3H31MoES.jpg",
                    VoteAverage = 8.3,
                    VoteCount = 22186,
                    Popularity = 29.108,
                    GenresIds = [28, 878, 53],
                    OriginalLanguage = "en",
                    Adult = false
                },

                new()
                {
                    Id = 497582,
                    Title = "Inception: The Cobol Job",
                    OriginalTitle = "Inception: The Cobol Job",
                    Overview = "This Inception prequel unfolds courtesy of a beautiful Motion Comic...",
                    ReleaseDate = DateTime.Parse("2010-12-07"),
                    PosterPath = "/jS71fcCxTaXSKnz6c11lJr4bFr6.jpg",
                    BackdropPath = null,
                    VoteAverage = 7.4,
                    VoteCount = 35,
                    Popularity = 3.714,
                    GenresIds = [16, 28, 878, 53],
                    OriginalLanguage = "en",
                    Adult = false
                }
            ]
        };
    }

    /// <summary>
    /// Creates a sample TV show search response for testing
    /// </summary>
    public static TmdbPaginatedResponse<TmdbTvShow> GetSampleTvShowSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 2,
            TotalPages = 1,
            Results =
            [
                new()
                {
                    Id = 1396,
                    Name = "Breaking Bad",
                    OriginalName = "Breaking Bad",
                    Overview =
                        "When Walter White, a New Mexico chemistry teacher, is diagnosed with Stage III cancer...",
                    FirstAirDate = DateTime.Parse("2008-01-20"),
                    PosterPath = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg",
                    BackdropPath = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
                    VoteAverage = 8.9,
                    VoteCount = 8007,
                    Popularity = 370.595,
                    GenreIds = [18, 80],
                    OriginCountry = ["US"],
                    OriginalLanguage = "en",
                    MediaType = "tv"
                },

                new()
                {
                    Id = 60625,
                    Name = "Better Call Saul",
                    OriginalName = "Better Call Saul",
                    Overview = "Six years before Saul Goodman meets Walter White...",
                    FirstAirDate = DateTime.Parse("2015-02-08"),
                    PosterPath = "/fC2HDm5t0kHl7mTm7jxMR31j7Qa.jpg",
                    BackdropPath = "/9faGSFi5jam6pDWGNd0p8JcJgXQ.jpg",
                    VoteAverage = 8.7,
                    VoteCount = 4471,
                    Popularity = 169.683,
                    GenreIds = [18, 35, 80],
                    OriginCountry = ["US"],
                    OriginalLanguage = "en",
                    MediaType = "tv"
                }
            ]
        };
    }

    /// <summary>
    /// Creates a sample person search response for testing
    /// </summary>
    public static TmdbPaginatedResponse<TmdbPerson> GetSamplePersonSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 1,
            TotalPages = 1,
            Results =
            [
                new()
                {
                    Id = 6193,
                    Name = "Leonardo DiCaprio",
                    KnownForDepartment = "Acting",
                    ProfilePath = "/wo2hJpn04vbtmh0B9utCFdsQhxM.jpg",
                    Popularity = 45.824,
                    Adult = false
                }
            ]
        };
    }

    /// <summary>
    /// Creates a sample multi-search response for testing
    /// Note: TmdbMultiSearch uses a tuple structure that makes it complex to mock
    /// This is simplified for testing purposes
    /// </summary>
    public static TmdbPaginatedResponse<TmdbMultiSearch> GetSampleMultiSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 3,
            TotalPages = 1,
            Results = []
        };
    }

    /// <summary>
    /// Creates a sample collection search response for testing
    /// </summary>
    public static TmdbPaginatedResponse<TmdbCollection> GetSampleCollectionSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 2,
            TotalPages = 1,
            Results =
            [
                new()
                {
                    Id = 86311,
                    Name = "The Avengers Collection",
                    PosterPath = "/yFSIUVTCvgYrpalUktulvk3Gi5Y.jpg",
                    BackdropPath = "/zuW6fOiusv4X9nnW3paHGfXcSll.jpg"
                },

                new()
                {
                    Id = 131295,
                    Name = "Captain America Collection",
                    PosterPath = "/3jWmSzF6OF3VdXjmvJfhvnM7iU.jpg",
                    BackdropPath = "/3jWmSzF6OF3VdXjmvJfhvnM7iU.jpg"
                }
            ]
        };
    }

    /// <summary>
    /// Creates a sample network search response for testing
    /// </summary>
    public static TmdbPaginatedResponse<TmdbNetwork> GetSampleNetworkSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 2,
            TotalPages = 1,
            Results =
            [
                new()
                {
                    Id = 213,
                    Name = "Netflix",
                    LogoPath = "/wwemzKWzjKYJFfCeiB57q3r4Bcm.png",
                    OriginCountry = "US"
                },

                new()
                {
                    Id = 49,
                    Name = "HBO",
                    LogoPath = "/tuomPhY2UuLiEIKfQLw3TvXKEYf.png",
                    OriginCountry = "US"
                }
            ]
        };
    }

    /// <summary>
    /// Creates a sample keyword search response for testing
    /// </summary>
    public static TmdbPaginatedResponse<TmdbKeyword> GetSampleKeywordSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 2,
            TotalPages = 1,
            Results =
            [
                new()
                {
                    Id = 9715,
                    Name = "superhero"
                },

                new()
                {
                    Id = 163528,
                    Name = "marvel cinematic universe"
                }
            ]
        };
    }

    /// <summary>
    /// Creates an empty response for testing error conditions
    /// </summary>
    public static TmdbPaginatedResponse<TmdbMovie> GetEmptyMovieSearchResponse()
    {
        return new()
        {
            Page = 1,
            TotalResults = 0,
            TotalPages = 0,
            Results = []
        };
    }
}
