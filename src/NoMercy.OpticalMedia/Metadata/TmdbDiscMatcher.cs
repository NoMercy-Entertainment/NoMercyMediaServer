using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Looks up TMDB candidates for a disc. Strategy:
///   1. Take the disc volume label (e.g. <c>Avatar_Book_1_Disc_1</c>),
///      normalise underscores → spaces, strip <c>disc N</c> suffixes.
///   2. Search both Movies and TvShows on the leading title segment.
///   3. Score by blended confidence: 60 % string similarity + 40 % duration
///      delta against TMDB's runtime field (±2 min = high score).
///   4. For TV matches, run a second pass against the season's episode list
///      to identify which specific episode the disc contains.
///   5. Return all viable candidates as <see cref="MetadataMatch"/> with
///      confidence — the UI lets the user pick if the top score is below
///      a confidence threshold.
/// </summary>
public sealed partial class TmdbDiscMatcher(ILogger<TmdbDiscMatcher> logger) : IDiscMetadataResolver
{
    private const int MaxCandidatesPerType = 5;

    /// <summary>High-confidence threshold above which a match is auto-applied.</summary>
    public const double AutoApplyThreshold = 0.85;

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

        int discDurationSec = disc.MainTitleDurationSec;

        List<MetadataMatch> all = new();
        all.AddRange(await SearchAsync(fullQuery, MediaType.Movie, ct, discDurationSec));
        all.AddRange(await SearchAsync(fullQuery, MediaType.TvShow, ct, discDurationSec));

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
                all.AddRange(await SearchAsync(firstWord, MediaType.Movie, ct, discDurationSec));
                all.AddRange(await SearchAsync(firstWord, MediaType.TvShow, ct, discDurationSec));
            }
        }

        // For TV matches, attempt per-episode resolution when the disc
        // duration is known, so the confirm endpoint can auto-set season/episode.
        List<MetadataMatch> resolved = new();
        foreach (MetadataMatch match in all)
        {
            if (match.Type == MediaType.TvShow && discDurationSec > 0 && match.ExternalId != null)
            {
                MetadataMatch enriched = await TryResolveEpisodeAsync(match, discDurationSec, ct);
                resolved.Add(enriched);
            }
            else
            {
                resolved.Add(match);
            }
        }

        logger.LogInformation(
            "TMDB resolve for '{Query}' (duration={Sec}s): {Count} candidates total",
            fullQuery,
            discDurationSec,
            resolved.Count
        );

        return resolved.OrderByDescending(m => m.Confidence).ToArray();
    }

    // Satisfies IDiscMetadataResolver — delegates to the duration-aware overload
    // with discDurationSec=0 so the blend falls back to string similarity only.
    public Task<MetadataMatch[]> SearchAsync(string query, MediaType type, CancellationToken ct) =>
        SearchAsync(query, type, ct, discDurationSec: 0);

    internal async Task<MetadataMatch[]> SearchAsync(
        string query,
        MediaType type,
        CancellationToken ct,
        int discDurationSec
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            return type switch
            {
                MediaType.Movie => await SearchMoviesAsync(query, discDurationSec),
                MediaType.TvShow => await SearchTvShowsAsync(query, discDurationSec),
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

    private async Task<MetadataMatch[]> SearchMoviesAsync(string query, int discDurationSec)
    {
        TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbMovie>? response = await client.Movie(query);
        if (response?.Results is null || response.Results.Count == 0)
            return [];

        List<MetadataMatch> matches = new();
        int rank = 0;
        foreach (TmdbMovie movie in response.Results.Take(MaxCandidatesPerType))
        {
            int? runtimeMin = await FetchMovieRuntimeAsync(movie.Id);

            double confidence = BlendConfidence(
                query,
                movie.Title ?? movie.OriginalTitle ?? "",
                rank,
                discDurationSec,
                runtimeMin
            );

            matches.Add(
                new MetadataMatch(
                    Source: "tmdb",
                    Confidence: confidence,
                    Title: movie.Title ?? movie.OriginalTitle ?? string.Empty,
                    Year: ParseYear(movie.ReleaseDate),
                    PosterUrl: PosterUrl(movie.PosterPath),
                    ExternalId: movie.Id.ToString(CultureInfo.InvariantCulture),
                    Type: MediaType.Movie,
                    Runtime: runtimeMin,
                    BackdropUrl: BackdropUrl(movie.BackdropPath)
                )
            );
            rank++;
        }

        return matches.ToArray();
    }

    private async Task<MetadataMatch[]> SearchTvShowsAsync(string query, int discDurationSec)
    {
        TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbTvShow>? response = await client.TvShow(query);
        if (response?.Results is null || response.Results.Count == 0)
            return [];

        List<MetadataMatch> matches = new();
        int rank = 0;
        foreach (TmdbTvShow show in response.Results.Take(MaxCandidatesPerType))
        {
            int? episodeRunMin = await FetchTvEpisodeRunTimeAsync(show.Id);

            double confidence = BlendConfidence(
                query,
                show.Name ?? show.OriginalName ?? "",
                rank,
                discDurationSec,
                episodeRunMin
            );

            matches.Add(
                new MetadataMatch(
                    Source: "tmdb",
                    Confidence: confidence,
                    Title: show.Name ?? show.OriginalName ?? string.Empty,
                    Year: ParseYear(show.FirstAirDate),
                    PosterUrl: PosterUrl(show.PosterPath),
                    ExternalId: show.Id.ToString(CultureInfo.InvariantCulture),
                    Type: MediaType.TvShow,
                    Runtime: episodeRunMin,
                    BackdropUrl: BackdropUrl(show.BackdropPath)
                )
            );
            rank++;
        }

        return matches.ToArray();
    }

    /// <summary>
    /// For a TV-show match, fetches season details to find which season/episode
    /// best explains <paramref name="discDurationSec"/>. Uses the show-level
    /// EpisodeRunTime as a per-episode estimate. Falls back to the unmodified
    /// match when TMDB returns no usable runtime data.
    /// </summary>
    private async Task<MetadataMatch> TryResolveEpisodeAsync(
        MetadataMatch match,
        int discDurationSec,
        CancellationToken ct
    )
    {
        if (!int.TryParse(match.ExternalId, out int showId))
            return match;

        try
        {
            TmdbTvClient tvClient = new(showId);
            TmdbTvShowDetails? showDetails = await tvClient.Details();
            if (showDetails?.Seasons is null || showDetails.Seasons.Count == 0)
                return match;

            int showRunMin = showDetails.EpisodeRunTime?.FirstOrDefault() ?? 0;
            if (showRunMin <= 0)
                return match;

            double showRunSec = showRunMin * 60.0;

            int bestSeasonNumber = 0;
            int bestEpisodeNumber = 1;
            double bestDelta = double.MaxValue;

            foreach (TmdbSeason season in showDetails.Seasons.Where(s => s.SeasonNumber > 0))
            {
                ct.ThrowIfCancellationRequested();
                TmdbSeasonClient seasonClient = new(showId, season.SeasonNumber);
                TmdbSeasonDetails? seasonDetails = await seasonClient.Details();
                if (seasonDetails?.Episodes is null || seasonDetails.Episodes.Length == 0)
                    continue;

                int episodeCount = seasonDetails.Episodes.Length;

                // Single-episode disc: each episode slot is a candidate.
                for (int epIdx = 1; epIdx <= episodeCount; epIdx++)
                {
                    double delta = Math.Abs(discDurationSec - showRunSec);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestSeasonNumber = season.SeasonNumber;
                        bestEpisodeNumber = epIdx;
                    }
                }

                // Full-season disc: total duration ≈ episodeCount × showRunSec.
                double fullDelta = Math.Abs(discDurationSec - (episodeCount * showRunSec));
                if (fullDelta < bestDelta)
                {
                    bestDelta = fullDelta;
                    bestSeasonNumber = season.SeasonNumber;
                    bestEpisodeNumber = 1;
                }
            }

            if (bestSeasonNumber == 0)
                return match;

            logger.LogInformation(
                "TV episode resolution for show {Id}: best match S{Season}E{Episode} (delta={Delta}s)",
                showId,
                bestSeasonNumber,
                bestEpisodeNumber,
                bestDelta
            );

            return match with
            {
                SeasonNumber = bestSeasonNumber,
                EpisodeNumber = bestEpisodeNumber,
            };
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                ex,
                "Episode resolution failed for show {Id}: {Message}",
                showId,
                ex.Message
            );
            return match;
        }
    }

    private static async Task<int?> FetchMovieRuntimeAsync(int movieId)
    {
        try
        {
            TmdbMovieClient client = new(movieId);
            TmdbMovieDetails? details = await client.Details();
            return details?.Runtime > 0 ? details.Runtime : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int?> FetchTvEpisodeRunTimeAsync(int showId)
    {
        try
        {
            TmdbTvClient client = new(showId);
            TmdbTvShowDetails? details = await client.Details();
            int? firstRuntime = details?.EpisodeRunTime?.FirstOrDefault();
            return firstRuntime is { } runtime && runtime > 0 ? runtime : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Blends string similarity (60 %) with duration proximity (40 %).
    /// When <paramref name="discDurationSec"/> is zero or
    /// <paramref name="runtimeMin"/> is null the duration component is omitted
    /// and the weight shifts entirely to string similarity.
    /// </summary>
    internal static double BlendConfidence(
        string query,
        string candidate,
        int rank,
        int discDurationSec,
        int? runtimeMin
    )
    {
        double similarity = NormalizedSimilarity(query, candidate);
        double rankPenalty = Math.Max(0, 1.0 - (rank * 0.1));
        double strScore = similarity * rankPenalty;

        if (discDurationSec <= 0 || runtimeMin is null or 0)
            return Math.Round(strScore, 4);

        double runtimeSec = runtimeMin.Value * 60.0;
        double durDelta = Math.Abs(discDurationSec - runtimeSec);
        double durScore = 1.0 - Math.Clamp(durDelta / runtimeSec, 0.0, 1.0);

        double blended = (0.6 * strScore) + (0.4 * durScore);
        return Math.Round(blended, 4);
    }

    /// <summary>
    /// Volume labels come from the disc as <c>Avatar_Book_1_Disc_1</c>
    /// — normalise to <c>Avatar Book 1</c> by replacing separators with
    /// spaces and stripping the trailing <c>disc N</c> token.
    /// </summary>
    internal static string NormalizeLabel(string label)
    {
        string spaced = label.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ');
        spaced = MultiSpaceRegex().Replace(spaced, " ").Trim();
        spaced = DiscSuffixRegex().Replace(spaced, "").Trim();
        return spaced;
    }

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

    private static string? BackdropUrl(string? backdropPath)
    {
        if (string.IsNullOrEmpty(backdropPath))
            return null;
        return $"https://image.tmdb.org/t/p/w1280{backdropPath}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"(\s+|^)disc\s*\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscSuffixRegex();
}
