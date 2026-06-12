using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Data.Repositories;

public interface IRecommendationRepository
{
    Task<List<RecommendationCandidateDto>> GetUnownedMovieRecommendationsAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetUnownedTvRecommendationsAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetUnownedAnimeRecommendationsAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetUnownedMovieSimilarAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetUnownedTvSimilarAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetUnownedAnimeSimilarAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeTvCandidatesAsync(
        MediaContext context,
        Guid userId,
        Dictionary<int, List<int>> movieKeywordMap,
        int minSharedKeywords = 3,
        int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeAnimeCandidatesAsync(
        MediaContext context,
        Guid userId,
        Dictionary<int, List<int>> movieKeywordMap,
        int minSharedKeywords = 3,
        int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default
    );

    Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeMovieCandidatesAsync(
        MediaContext context,
        Guid userId,
        Dictionary<int, List<int>> tvKeywordMap,
        int minSharedKeywords = 3,
        int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default
    );

    Task<List<UserAffinitySourceDto>> GetUserMovieAffinityDataAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<UserAffinitySourceDto>> GetUserTvAffinityDataAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<UserAffinitySourceDto>> GetUserAnimeAffinityDataAsync(
        MediaContext context,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Dictionary<int, List<int>>> GetGenresForMovieIdsAsync(
        MediaContext context,
        List<int> movieIds,
        CancellationToken ct = default
    );

    Task<Dictionary<int, List<int>>> GetGenresForTvIdsAsync(
        MediaContext context,
        List<int> tvIds,
        CancellationToken ct = default
    );

    Task<(List<Movie> Movies, string? ColorPalette)> GetSourceMoviesForMediaAsync(
        MediaContext context,
        Guid userId,
        int mediaId,
        CancellationToken ct = default
    );

    Task<(List<Tv> TvShows, string? ColorPalette)> GetSourceTvShowsForMediaAsync(
        MediaContext context,
        Guid userId,
        int mediaId,
        CancellationToken ct = default
    );

    Task<List<Movie>> GetKeywordMovieSourcesForMovieAsync(
        MediaContext context,
        Guid userId,
        int movieId,
        HashSet<int> excludeIds,
        CancellationToken ct = default
    );

    Task<List<Tv>> GetKeywordTvSourcesForTvAsync(
        MediaContext context,
        Guid userId,
        int tvId,
        HashSet<int> excludeIds,
        CancellationToken ct = default
    );

    Task<List<Movie>> GetCrossTypeMovieSourcesForTvAsync(
        MediaContext context,
        Guid userId,
        int tvId,
        CancellationToken ct = default
    );

    Task<List<Tv>> GetCrossTypeTvSourcesForMovieAsync(
        MediaContext context,
        Guid userId,
        int movieId,
        CancellationToken ct = default
    );

    Task<RecommendationDiagnosticsDto> GetDiagnosticsAsync(CancellationToken ct = default);
}
