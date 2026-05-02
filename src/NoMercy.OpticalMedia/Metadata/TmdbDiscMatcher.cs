using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Looks up TMDB candidates for a disc. Strategy:
///   1. Take the disc volume label (e.g. <c>Avatar_Book_1_Disc_1</c>),
///      normalise underscores → spaces, strip <c>disc N</c> suffixes.
///   2. Search both Movies and TvShows on the leading title segment.
///   3. Score by string similarity + result rank from TMDB.
///   4. Return all viable candidates as <see cref="MetadataMatch"/> with
///      confidence — the UI lets the user pick if the top score is below
///      a confidence threshold.
/// </summary>
public sealed partial class TmdbDiscMatcher(ILogger<TmdbDiscMatcher> logger) : IDiscMetadataResolver
{
    private const int MaxCandidatesPerType = 5;

    public async Task<MetadataMatch[]> ResolveAsync(DiscInfo disc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disc.DiscLabel))
        {
            logger.LogInformation(
                "TMDB resolve skipped — disc has no label (type={Type})",
                disc.Type
            );
            return [];
        }

        string fullQuery = NormalizeLabel(disc.DiscLabel);
        if (string.IsNullOrWhiteSpace(fullQuery))
        {
            logger.LogInformation(
                "TMDB resolve skipped — normalised label is empty (raw={Raw})",
                disc.DiscLabel
            );
            return [];
        }

        // Try the full normalised query first ("Avatar Book 1"). If TMDB
        // doesn't have anything indexed under that exact phrase, fall back
        // to just the first word ("Avatar") which usually pulls the show
        // root with all season variants. The user can then pick from the
        // candidate list.
        List<MetadataMatch> all = new();
        all.AddRange(await SearchAsync(fullQuery, MediaType.Movie, ct));
        all.AddRange(await SearchAsync(fullQuery, MediaType.TvShow, ct));

        if (all.Count == 0)
        {
            string firstWord = fullQuery.Split(' ', 2)[0];
            if (!firstWord.Equals(fullQuery, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "TMDB resolve fallback for '{Query}' → '{FirstWord}'",
                    fullQuery,
                    firstWord
                );
                all.AddRange(await SearchAsync(firstWord, MediaType.Movie, ct));
                all.AddRange(await SearchAsync(firstWord, MediaType.TvShow, ct));
            }
        }

        logger.LogInformation(
            "TMDB resolve for '{Query}': {Count} candidates total",
            fullQuery,
            all.Count
        );

        return all.OrderByDescending(m => m.Confidence).ToArray();
    }

    public async Task<MetadataMatch[]> SearchAsync(
        string query,
        MediaType type,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            return type switch
            {
                MediaType.Movie => await SearchMoviesAsync(query),
                MediaType.TvShow => await SearchTvShowsAsync(query),
                _ => [],
            };
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                ex,
                "TMDB search failed for {Type} '{Query}': {Message}",
                type,
                query,
                ex.Message
            );
            return [];
        }
    }

    private async Task<MetadataMatch[]> SearchMoviesAsync(string query)
    {
        TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbMovie>? response = await client.Movie(query);
        if (response?.Results is null || response.Results.Count == 0)
            return [];

        return response
            .Results.Take(MaxCandidatesPerType)
            .Select(
                (movie, rank) =>
                    new MetadataMatch(
                        Source: "tmdb",
                        Confidence: ScoreMatch(
                            query,
                            movie.Title ?? movie.OriginalTitle ?? "",
                            rank
                        ),
                        Title: movie.Title ?? movie.OriginalTitle ?? string.Empty,
                        Year: ParseYear(movie.ReleaseDate),
                        PosterUrl: PosterUrl(movie.PosterPath),
                        ExternalId: movie.Id.ToString(CultureInfo.InvariantCulture),
                        Type: MediaType.Movie
                    )
            )
            .ToArray();
    }

    private async Task<MetadataMatch[]> SearchTvShowsAsync(string query)
    {
        TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbTvShow>? response = await client.TvShow(query);
        if (response?.Results is null || response.Results.Count == 0)
            return [];

        return response
            .Results.Take(MaxCandidatesPerType)
            .Select(
                (show, rank) =>
                    new MetadataMatch(
                        Source: "tmdb",
                        Confidence: ScoreMatch(query, show.Name ?? show.OriginalName ?? "", rank),
                        Title: show.Name ?? show.OriginalName ?? string.Empty,
                        Year: ParseYear(show.FirstAirDate),
                        PosterUrl: PosterUrl(show.PosterPath),
                        ExternalId: show.Id.ToString(CultureInfo.InvariantCulture),
                        Type: MediaType.TvShow
                    )
            )
            .ToArray();
    }

    /// <summary>
    /// Volume labels come from the disc as <c>Avatar_Book_1_Disc_1</c>
    /// — normalise to <c>Avatar Book 1</c> by replacing separators with
    /// spaces and stripping the trailing <c>disc N</c> token. The leading
    /// "core" of the title is what TMDB will know about; the disc/book
    /// number is only used afterwards to pick the right season.
    /// </summary>
    internal static string NormalizeLabel(string label)
    {
        string spaced = label.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ');
        spaced = MultiSpaceRegex().Replace(spaced, " ").Trim();
        spaced = DiscSuffixRegex().Replace(spaced, "").Trim();
        return spaced;
    }

    /// <summary>
    /// Scores how confidently a TMDB result matches the disc query.
    /// Combines: (a) string similarity of the result's title against the
    /// query, (b) result-list rank (TMDB returns most-relevant first).
    /// Range 0.0 to 1.0.
    /// </summary>
    private static double ScoreMatch(string query, string candidate, int rank)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(candidate))
            return 0;

        double similarity = NormalizedSimilarity(query, candidate);
        double rankPenalty = Math.Max(0, 1.0 - (rank * 0.1));
        return Math.Round(similarity * rankPenalty, 4);
    }

    /// <summary>
    /// Cheap normalised similarity: count of shared lowercase tokens
    /// divided by the union size. Good enough for "Avatar Book 1" vs
    /// "Avatar: The Last Airbender" — they share "Avatar" but the TMDB
    /// title has more tokens, so it scores 1/4 = 0.25 raw. Combined with
    /// rank=0 (top hit) it becomes 0.25 * 1.0 = 0.25 confidence.
    /// </summary>
    private static double NormalizedSimilarity(string a, string b)
    {
        HashSet<string> aTokens = new(
            a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );
        HashSet<string> bTokens = new(
            b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );
        if (aTokens.Count == 0 || bTokens.Count == 0)
            return 0;

        int intersection = aTokens.Intersect(bTokens).Count();
        int union = aTokens.Union(bTokens).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static int? ParseYear(DateTime? date) => date?.Year;

    private static string? PosterUrl(string? posterPath)
    {
        if (string.IsNullOrEmpty(posterPath))
            return null;
        return $"https://image.tmdb.org/t/p/w500{posterPath}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"(\s+|^)disc\s*\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscSuffixRegex();
}
