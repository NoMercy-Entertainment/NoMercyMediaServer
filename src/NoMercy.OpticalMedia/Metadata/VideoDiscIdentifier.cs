// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Identifies DVD and Blu-ray discs by searching TMDB. Matches are ranked
/// by a blended confidence score (string similarity + runtime proximity).
///
/// For TV discs a second pass walks the season/episode list to find the best
/// season/episode fit based on disc duration.
///
/// Implements <see cref="IDiscIdentifier"/>; dispatched by
/// <see cref="DiscIdentificationService"/>.
/// </summary>
public sealed partial class VideoDiscIdentifier(ILogger<VideoDiscIdentifier> logger)
    : IDiscIdentifier
{
    private const int MaxCandidatesPerType = 5;

    /// <summary>Confidence threshold above which a match is auto-applied.</summary>
    public const double AutoApplyThreshold = 0.85;

    public bool CanHandle(OpticalDiscType type) =>
        type == OpticalDiscType.Dvd || type == OpticalDiscType.BluRay;

    public async Task<DiscIdentification> IdentifyAsync(DiscInfo disc, CancellationToken ct)
    {
        // Prefer the embedded Blu-ray title (bdmt_*.xml) over the raw volume label.
        string? querySource = disc.DiscTitle ?? disc.DiscLabel;

        if (string.IsNullOrWhiteSpace(value: querySource))
        {
            logger.LogInformation(
                message: "VideoDiscIdentifier skipped — disc has no label or embedded title (type={Type})",
                args: disc.Type
            );
            return NeedsManual();
        }

        string fullQuery = NormalizeLabel(label: querySource);
        if (string.IsNullOrWhiteSpace(value: fullQuery))
        {
            logger.LogInformation(
                message: "VideoDiscIdentifier skipped — normalised query is empty (raw={Raw})",
                args: querySource
            );
            return NeedsManual();
        }

        int discDurationSec = disc.MainTitleDurationSec;

        List<DiscCandidate> all = [];
        all.AddRange(collection: await SearchAsync(query: fullQuery, type: MediaType.Movie, ct: ct, discDurationSec: discDurationSec));
        all.AddRange(collection: await SearchAsync(query: fullQuery, type: MediaType.TvShow, ct: ct, discDurationSec: discDurationSec));

        if (all.Count == 0)
        {
            string firstWord = fullQuery.Split(separator: ' ', count: 2)[0];
            if (!firstWord.Equals(value: fullQuery, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    message: "VideoDiscIdentifier fallback '{Query}' → '{FirstWord}'", args: [fullQuery, firstWord]
                );
                all.AddRange(collection: await SearchAsync(query: firstWord, type: MediaType.Movie, ct: ct, discDurationSec: discDurationSec));
                all.AddRange(collection: await SearchAsync(query: firstWord, type: MediaType.TvShow, ct: ct, discDurationSec: discDurationSec));
            }
        }

        if (all.Count == 0)
        {
            logger.LogInformation(
                message: "VideoDiscIdentifier found no TMDB candidates for '{Query}'",
                args: fullQuery
            );
            return NeedsManual();
        }

        // For TV matches, attempt per-episode resolution when disc duration is known.
        List<DiscCandidate> resolved = [];
        foreach (DiscCandidate candidate in all)
        {
            if (
                candidate.Source == "tmdb"
                && !string.IsNullOrEmpty(value: candidate.StableId)
                && discDurationSec > 0
            )
            {
                // Determine whether this is a TV candidate by trying to parse
                // the StableId and checking back against results from SearchAsync.
                // We use the SeasonNumber field as the TV discriminator.
                DiscCandidate enriched = await TryResolveEpisodeAsync(
                    candidate: candidate,
                    discDurationSec: discDurationSec,
                    ct: ct
                );
                resolved.Add(item: enriched);
            }
            else
            {
                resolved.Add(item: candidate);
            }
        }

        DiscCandidate[] ranked = resolved.OrderByDescending(keySelector: c => c.Confidence).ToArray();

        double topConfidence = ranked.Length > 0 ? ranked[0].Confidence : 0;
        bool autoApply = topConfidence >= AutoApplyThreshold;

        logger.LogInformation(
            message: "VideoDiscIdentifier '{Query}' (duration={Sec}s): {Count} candidates, topConfidence={Conf:F4}, autoApply={Auto}", args: [fullQuery, discDurationSec, ranked.Length, topConfidence, autoApply]
        );

        return new(
            Kind: MediaKind.Movie,
            Candidates: ranked,
            TopConfidence: topConfidence,
            AutoApply: autoApply,
            NeedsManualAssignment: false
        );
    }

    private async Task<IEnumerable<DiscCandidate>> SearchAsync(
        string query,
        MediaType type,
        CancellationToken ct,
        int discDurationSec
    )
    {
        try
        {
            return type switch
            {
                MediaType.Movie => await SearchMoviesAsync(query: query, discDurationSec: discDurationSec),
                MediaType.TvShow => await SearchTvShowsAsync(query: query, discDurationSec: discDurationSec),
                _ => [],
            };
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                exception: ex,
                message: "TMDB search failed for {Type} '{Query}': {Message}", args: [type, query, ex.Message]
            );
            return [];
        }
    }

    /// <summary>
    /// Manual title search — used by the dashboard's search-box endpoint.
    /// Searches both Movies and TvShows and returns ranked candidates.
    /// </summary>
    public async Task<DiscCandidate[]> SearchAsync(
        string query,
        MediaType type,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: query))
            return [];

        try
        {
            return type switch
            {
                MediaType.Movie => (await SearchMoviesAsync(query: query, discDurationSec: 0)).ToArray(),
                MediaType.TvShow => (await SearchTvShowsAsync(query: query, discDurationSec: 0)).ToArray(),
                _ => [],
            };
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                exception: ex,
                message: "TMDB manual search failed for {Type} '{Query}': {Message}", args: [type, query, ex.Message]
            );
            return [];
        }
    }

    private async Task<IEnumerable<DiscCandidate>> SearchMoviesAsync(
        string query,
        int discDurationSec
    )
    {
        TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbMovie>? response = await client.Movie(query: query);
        if (response?.Results is null || response.Results.Count == 0)
            return [];

        List<DiscCandidate> matches = [];
        int rank = 0;
        foreach (TmdbMovie movie in response.Results.Take(count: MaxCandidatesPerType))
        {
            int? runtimeMin = await FetchMovieRuntimeAsync(movieId: movie.Id);
            double confidence = BlendConfidence(
                query: query,
                candidate: movie.Title ?? movie.OriginalTitle ?? "",
                rank: rank,
                discDurationSec: discDurationSec,
                runtimeMin: runtimeMin
            );
            matches.Add(
                item: new(
                    Source: "tmdb",
                    StableId: movie.Id.ToString(provider: CultureInfo.InvariantCulture),
                    Title: movie.Title ?? movie.OriginalTitle ?? string.Empty,
                    Year: ParseYear(date: movie.ReleaseDate),
                    PosterUrl: PosterUrl(posterPath: movie.PosterPath),
                    BackdropUrl: BackdropUrl(backdropPath: movie.BackdropPath),
                    Confidence: confidence,
                    Type: MediaType.Movie
                )
            );
            rank++;
        }
        return matches;
    }

    private async Task<IEnumerable<DiscCandidate>> SearchTvShowsAsync(
        string query,
        int discDurationSec
    )
    {
        TmdbSearchClient client = new();
        TmdbPaginatedResponse<TmdbTvShow>? response = await client.TvShow(query: query);
        if (response?.Results is null || response.Results.Count == 0)
            return [];

        List<DiscCandidate> matches = [];
        int rank = 0;
        foreach (TmdbTvShow show in response.Results.Take(count: MaxCandidatesPerType))
        {
            int? episodeRunMin = await FetchTvEpisodeRunTimeAsync(showId: show.Id);
            double confidence = BlendConfidence(
                query: query,
                candidate: show.Name ?? show.OriginalName ?? "",
                rank: rank,
                discDurationSec: discDurationSec,
                runtimeMin: episodeRunMin
            );
            matches.Add(
                item: new(
                    Source: "tmdb",
                    StableId: show.Id.ToString(provider: CultureInfo.InvariantCulture),
                    Title: show.Name ?? show.OriginalName ?? string.Empty,
                    Year: ParseYear(date: show.FirstAirDate),
                    PosterUrl: PosterUrl(posterPath: show.PosterPath),
                    BackdropUrl: BackdropUrl(backdropPath: show.BackdropPath),
                    Confidence: confidence,
                    Type: MediaType.TvShow
                )
            );
            rank++;
        }
        return matches;
    }

    /// <summary>
    /// For a candidate that resolved from the TV-show search, attempts to
    /// determine which season and starting episode the disc contains.
    ///
    /// The disc duration is compared against hypotheses:
    ///   - Single-episode: disc ≈ 1 × episodeRuntime
    ///   - Multi-episode:  disc ≈ N × episodeRuntime  (N episodes starting at ep 1)
    ///
    /// When individual episode runtimes are not available from TMDB, the
    /// show-level EpisodeRunTime is used as a uniform estimate.
    ///
    /// BUG FIX: the original implementation used a constant delta
    /// (<c>Math.Abs(discDurationSec - showRunSec)</c>) for every episode
    /// slot, so the comparison never changed across loop iterations and
    /// always produced episode 1. The corrected logic makes the expected
    /// duration depend on how many episodes fit (<c>epIdx * showRunSec</c>),
    /// producing a real per-hypothesis delta.
    /// </summary>
    private async Task<DiscCandidate> TryResolveEpisodeAsync(
        DiscCandidate candidate,
        int discDurationSec,
        CancellationToken ct
    )
    {
        if (!int.TryParse(s: candidate.StableId, result: out int showId))
            return candidate;

        try
        {
            TmdbTvClient tvClient = new(id: showId);
            TmdbTvShowDetails? showDetails = await tvClient.Details();
            if (showDetails?.Seasons is null || showDetails.Seasons.Count == 0)
                return candidate;

            int showRunMin = showDetails.EpisodeRunTime?.FirstOrDefault() ?? 0;
            if (showRunMin <= 0)
                return candidate;

            double showRunSec = showRunMin * 60.0;

            int bestSeasonNumber = 0;
            int bestEpisodeCount = 1;
            double bestDelta = double.MaxValue;

            foreach (TmdbSeason season in showDetails.Seasons.Where(predicate: s => s.SeasonNumber > 0))
            {
                ct.ThrowIfCancellationRequested();

                TmdbSeasonClient seasonClient = new(tvId: showId, seasonNumber: season.SeasonNumber);
                TmdbSeasonDetails? seasonDetails = await seasonClient.Details();
                if (seasonDetails?.Episodes is null || seasonDetails.Episodes.Length == 0)
                    continue;

                int episodeCount = seasonDetails.Episodes.Length;

                // Test each hypothesis: disc contains episodes 1..epIdx
                // so expected total runtime = epIdx * showRunSec.
                for (int epIdx = 1; epIdx <= episodeCount; epIdx++)
                {
                    double expectedSec = epIdx * showRunSec;
                    double delta = Math.Abs(value: discDurationSec - expectedSec);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestSeasonNumber = season.SeasonNumber;
                        bestEpisodeCount = epIdx;
                    }
                }
            }

            if (bestSeasonNumber == 0)
                return candidate;

            logger.LogInformation(
                message: "TV episode resolution for show {Id}: best match S{Season}, {Count} episode(s) on disc (delta={Delta:F1}s)", args: [showId, bestSeasonNumber, bestEpisodeCount, bestDelta]
            );

            // EpisodeNumber = 1 always (first episode on the disc);
            // SeasonNumber is the resolved season.
            return candidate with
            {
                SeasonNumber = bestSeasonNumber,
                EpisodeNumber = 1,
            };
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                exception: ex,
                message: "Episode resolution failed for show {Id}: {Message}", args: [showId, ex.Message]
            );
            return candidate;
        }
    }

    private static async Task<int?> FetchMovieRuntimeAsync(int movieId)
    {
        try
        {
            TmdbMovieClient client = new(id: movieId);
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
            TmdbTvClient client = new(id: showId);
            TmdbTvShowDetails? details = await client.Details();
            int? firstRuntime = details?.EpisodeRunTime?.FirstOrDefault();
            return firstRuntime is { } runtime and > 0 ? runtime : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Blends string similarity (60 %) with duration proximity (40 %).
    /// When <paramref name="discDurationSec"/> is zero or
    /// <paramref name="runtimeMin"/> is null, the weight shifts entirely
    /// to string similarity.
    /// </summary>
    internal static double BlendConfidence(
        string query,
        string candidate,
        int rank,
        int discDurationSec,
        int? runtimeMin
    )
    {
        double similarity = NormalizedSimilarity(a: query, b: candidate);
        double rankPenalty = Math.Max(val1: 0, val2: 1.0 - (rank * 0.1));
        double strScore = similarity * rankPenalty;

        if (discDurationSec <= 0 || runtimeMin is null or 0)
            return Math.Round(value: strScore, digits: 4);

        double runtimeSec = runtimeMin.Value * 60.0;
        double durDelta = Math.Abs(value: discDurationSec - runtimeSec);
        double durScore = 1.0 - Math.Clamp(value: durDelta / runtimeSec, min: 0.0, max: 1.0);

        double blended = (0.6 * strScore) + (0.4 * durScore);
        return Math.Round(value: blended, digits: 4);
    }

    /// <summary>
    /// Volume labels come from the disc as e.g. <c>Avatar_Book_1_Disc_1</c>.
    /// Normalise to <c>Avatar Book 1</c> by replacing separators with spaces
    /// and stripping the trailing <c>disc N</c> token.
    /// </summary>
    internal static string NormalizeLabel(string label)
    {
        string spaced = label.Replace(oldChar: '_', newChar: ' ').Replace(oldChar: '.', newChar: ' ').Replace(oldChar: '-', newChar: ' ');
        spaced = MultiSpaceRegex().Replace(input: spaced, replacement: " ").Trim();
        spaced = DiscSuffixRegex().Replace(input: spaced, replacement: "").Trim();
        return spaced;
    }

    private static double NormalizedSimilarity(string a, string b)
    {
        HashSet<string> aTokens = new(
            collection: a.ToLowerInvariant().Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries)
        );
        HashSet<string> bTokens = new(
            collection: b.ToLowerInvariant().Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries)
        );
        if (aTokens.Count == 0 || bTokens.Count == 0)
            return 0;

        int intersection = aTokens.Intersect(second: bTokens).Count();
        int union = aTokens.Union(second: bTokens).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static int? ParseYear(DateTime? date) => date?.Year;

    private static string? PosterUrl(string? posterPath)
    {
        if (string.IsNullOrEmpty(value: posterPath))
            return null;
        return $"{TmdbImageClient.ImageBaseUrl}w500{posterPath}";
    }

    private static string? BackdropUrl(string? backdropPath)
    {
        if (string.IsNullOrEmpty(value: backdropPath))
            return null;
        return $"{TmdbImageClient.ImageBaseUrl}w1280{backdropPath}";
    }

    private static DiscIdentification NeedsManual() =>
        new(
            Kind: MediaKind.Movie,
            Candidates: [],
            TopConfidence: 0,
            AutoApply: false,
            NeedsManualAssignment: true
        );

    [GeneratedRegex(pattern: @"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(pattern: @"(\s+|^)disc\s*\d+\s*$", options: RegexOptions.IgnoreCase)]
    private static partial Regex DiscSuffixRegex();
}
