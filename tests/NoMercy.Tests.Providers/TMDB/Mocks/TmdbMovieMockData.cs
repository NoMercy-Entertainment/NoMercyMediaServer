using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Tests.Providers.TMDB.Mocks;

/// <summary>
/// Mock data provider for TMDB Movie API responses
/// Contains realistic test data based on actual TMDB API responses
/// </summary>
public static class TmdbMovieMockData
{
    /// <summary>
    /// Mock data for a popular movie (The Dark Knight - TMDB ID: 155)
    /// </summary>
    public static TmdbMovieDetails GetSampleMovieDetails()
    {
        return new()
        {
            Id = 155,
            Title = "The Dark Knight",
            OriginalTitle = "The Dark Knight",
            Overview = "Batman raises the stakes in his war on crime. With the help of Lt. Jim Gordon and District Attorney Harvey Dent, Batman sets out to dismantle the remaining criminal organizations that plague the streets. The partnership proves to be effective, but they soon find themselves prey to a reign of chaos unleashed by a rising criminal mastermind known to the terrified citizens of Gotham as the Joker.",
            Adult = false,
            BackdropPath = "/qhQnY2fUcQqOomvJWgUHDrjNPzO.jpg",
            PosterPath = "/qJ2tW6WMUDux911r6m7haRef0WH.jpg",
            Budget = 185000000,
            Revenue = 1005973645,
            Runtime = 152,
            Status = "Released",
            Tagline = "Welcome to a world without rules.",
            ReleaseDate = DateTime.Parse("2008-07-16"),
            OriginalLanguage = "en",
            Popularity = 123.456,
            VoteAverage = 9.0,
            VoteCount = 32000,
            Video = false,
            ImdbId = "tt0468569",
            Homepage = new("http://www.42entertainment.com/work/whysoserious")
        };
    }

    /// <summary>
    /// Mock data for a movie with minimal data
    /// </summary>
    public static TmdbMovieDetails GetMinimalMovieDetails()
    {
        return new()
        {
            Id = 999999,
            Title = "Test Movie",
            OriginalTitle = "Test Movie",
            Adult = false,
            Status = "Released",
            ReleaseDate = DateTime.Parse("2024-01-01"),
            OriginalLanguage = "en"
        };
    }

    /// <summary>
    /// Mock data for movie credits
    /// </summary>
    public static TmdbMovieCredits GetSampleMovieCredits()
    {
        return new()
        {
            Id = 155,
            Cast =
            [
                new()
                {
                    Id = 3894,
                    Name = "Christian Bale",
                    Character = "Bruce Wayne / Batman",
                    Order = 0,
                    CreditId = "52fe4781c3a36847f812f049",
                    Gender = 2,
                    KnownForDepartment = "Acting",
                    OriginalName = "Christian Bale",
                    Popularity = 42.45f,
                    ProfilePath = "/qCpZn2e3dimwbryLnqxZuI88PTi.jpg"
                },
                new()
                {
                    Id = 1199,
                    Name = "Heath Ledger",
                    Character = "Joker",
                    Order = 1,
                    CreditId = "52fe4781c3a36847f812f04b",
                    Gender = 2,
                    KnownForDepartment = "Acting",
                    OriginalName = "Heath Ledger",
                    Popularity = 38.12f,
                    ProfilePath = "/5Y9HnYYa9jF4NunY9lSgJGjSe8E.jpg"
                }
            ],
            Crew =
            [
                new()
                {
                    Id = 525,
                    Name = "Christopher Nolan",
                    Job = "Director",
                    Department = "Directing",
                    CreditId = "52fe4781c3a36847f812f041",
                    Gender = 2,
                    KnownForDepartment = "Directing",
                    OriginalName = "Christopher Nolan",
                    Popularity = 15.67f,
                    ProfilePath = "/xuAIuYSmsUzKlUMBFGVZaWsY3DZ.jpg"
                }
            ]
        };
    }

    /// <summary>
    /// Mock data for movie external IDs
    /// </summary>
    public static TmdbMovieExternalIds GetSampleMovieExternalIds()
    {
        return new()
        {
            Id = 155,
            ImdbId = "tt0468569",
            FacebookId = "TheDarkKnight",
            InstagramId = "thedarkknight",
            TwitterId = "thedarkknight"
        };
    }

    /// <summary>
    /// Mock data for an invalid/non-existent movie
    /// </summary>
    public static TmdbMovieDetails? GetInvalidMovieDetails()
    {
        return null;
    }

    /// <summary>
    /// Generate mock movie with specific ID
    /// </summary>
    public static TmdbMovieDetails GenerateMovieWithId(int id)
    {
        return new()
        {
            Id = id,
            Title = $"Test Movie {id}",
            OriginalTitle = $"Test Movie {id}",
            Adult = false,
            Status = "Released",
            ReleaseDate = DateTime.Parse("2024-01-01"),
            OriginalLanguage = "en",
            Overview = $"This is a test movie with ID {id}.",
            Popularity = id * 0.1,
            VoteAverage = Math.Min(10.0, id * 0.01 + 5.0),
            VoteCount = id * 10,
            Runtime = 90 + (id % 60)
        };
    }

    /// <summary>
    /// Mock data for movie appends response
    /// </summary>
    public static TmdbMovieAppends GetSampleMovieAppends()
    {
        TmdbMovieDetails movie = GetSampleMovieDetails();
        return new()
        {
            Id = movie.Id,
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle,
            Overview = movie.Overview,
            Adult = movie.Adult,
            BackdropPath = movie.BackdropPath,
            PosterPath = movie.PosterPath,
            Budget = movie.Budget,
            Revenue = movie.Revenue,
            Runtime = movie.Runtime,
            Status = movie.Status,
            Tagline = movie.Tagline,
            ReleaseDate = movie.ReleaseDate,
            OriginalLanguage = movie.OriginalLanguage,
            Popularity = movie.Popularity,
            VoteAverage = movie.VoteAverage,
            VoteCount = movie.VoteCount,
            Video = movie.Video,
            ImdbId = movie.ImdbId,
            Homepage = movie.Homepage,
            Credits = GetSampleMovieCredits(),
            ExternalIds = GetSampleMovieExternalIds()
        };
    }
}
