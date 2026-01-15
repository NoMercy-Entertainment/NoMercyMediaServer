using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Client.Mocks;
using NoMercy.Setup;

namespace NoMercy.Tests.Providers.TMDB;

/// <summary>
/// Base class for TMDB client tests providing common setup and mock data
/// </summary>
public abstract class TmdbTestBase : IDisposable
{
    protected readonly MovieResponseMocks MockDataProvider;
    protected bool Disposed;

    protected TmdbTestBase()
    {
        MockDataProvider = new();
        
        // Configure TMDB API token for tests
        SetupTmdbAuthentication();
    }

    /// <summary>
    /// Creates a valid movie ID for testing
    /// </summary>
    protected const int ValidMovieId = 155; // The Dark Knight

    /// <summary>
    /// Creates an invalid movie ID for testing
    /// </summary>
    protected const int InvalidMovieId = 999999;
    
    /// <summary>
    /// Creates a valid TV show ID for testing
    /// </summary>
    protected const int ValidTvShowId = 11890; // Goede Tijden, Slechte Tijden (GTST) - Active Dutch soap
    
    /// <summary>
    /// Creates a valid season number for testing
    /// </summary>
    protected const int ValidSeasonNumber = 54; // Current season (September 2025)
    
    /// <summary>
    /// Creates a valid episode number for testing
    /// </summary>
    protected const int ValidEpisodeNumber = 1;
    
    /// <summary>
    /// Creates a valid person ID for testing
    /// </summary>
    protected const int ValidPersonId = 6193; // Leonardo DiCaprio
    
    /// <summary>
    /// Creates a valid collection ID for testing
    /// </summary>
    protected const int ValidCollectionId = 263; // The Dark Knight Collection

    /// <summary>
    /// TMDB API Token from NoMercy API endpoint
    /// </summary>
    private const string TmdbApiToken = "eyJhbGciOiJIUzI1NiJ9.eyJhdWQiOiJlZDNiZjg2MGFkZWYwNTM3NzgzZTRhYmVlODZkNjVhZiIsInN1YiI6IjViNTE5MWQ3MGUwYTI2MjU5OTAwZmY0MyIsInNjb3BlcyI6WyJhcGlfcmVhZCJdLCJ2ZXJzaW9uIjoxfQ.QndOAaK4WKspNYRhVxp0yq1-plwoJR7iBcwQSn0NQJA";

    /// <summary>
    /// Sets up TMDB authentication for test execution
    /// </summary>
    protected static void SetupTmdbAuthentication()
    {
        // Set the TMDB token in ApiInfo for all TMDB clients to use
        ApiInfo.TmdbToken = TmdbApiToken;
    }

    /// <summary>
    /// Creates a TmdbMovieClient with mock data for testing
    /// </summary>
    /// <param name="movieId">The movie ID to use</param>
    /// <param name="language">The language to use (default: en-US)</param>
    /// <returns>A TmdbMovieClient instance with mocked data</returns>
    protected TmdbMovieClient CreateMockMovieClient(int movieId = ValidMovieId, string language = "en-US")
    {
        return new(movieId, null, MockDataProvider, language);
    }

    /// <summary>
    /// Creates a real TmdbMovieClient for integration testing
    /// Note: This will make real API calls
    /// </summary>
    /// <param name="movieId">The movie ID to use</param>
    /// <param name="language">The language to use (default: en-US)</param>
    /// <returns>A TmdbMovieClient instance without mocking</returns>
    protected TmdbMovieClient CreateRealMovieClient(int movieId = ValidMovieId, string language = "en-US")
    {
        return new(movieId, null, null, language);
    }
    
    /// <summary>
    /// Creates a TmdbSearchClient for testing
    /// </summary>
    /// <returns>A TmdbSearchClient instance</returns>
    protected static TmdbSearchClient CreateMockSearchClient()
    {
        // For now, return a real client - we can add mocking later if needed
        return new();
    }
    
    /// <summary>
    /// Creates a real TmdbSearchClient for integration testing
    /// Note: This will make real API calls
    /// </summary>
    /// <returns>A TmdbSearchClient instance</returns>
    protected static TmdbSearchClient CreateRealSearchClient()
    {
        return new();
    }
    
    /// <summary>
    /// Creates a TmdbTvClient for testing
    /// </summary>
    /// <param name="tvId">The TV show ID to use</param>
    /// <param name="language">The language to use (default: en-US)</param>
    /// <returns>A TmdbTvClient instance</returns>
    protected static TmdbTvClient CreateMockTvClient(int tvId = ValidTvShowId, string language = "en-US")
    {
        return new(tvId, null, language);
    }
    
    /// <summary>
    /// Creates a real TmdbTvClient for integration testing
    /// Note: This will make real API calls
    /// </summary>
    /// <param name="tvId">The TV show ID to use</param>
    /// <param name="language">The language to use (default: en-US)</param>
    /// <returns>A TmdbTvClient instance</returns>
    protected static TmdbTvClient CreateRealTvClient(int tvId = ValidTvShowId, string language = "en-US")
    {
        return new(tvId, null, language);
    }

    public virtual void Dispose()
    {
        if (Disposed) return;
        
        Disposed = true;
        GC.SuppressFinalize(this);
    }
}
