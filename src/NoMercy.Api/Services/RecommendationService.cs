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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Api.Services;

public class RecommendationService
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IMemoryCache _cache;
    private readonly IMovieMetadataProvider _movieMetadataProvider;
    private readonly ITvShowMetadataProvider _tvShowMetadataProvider;

    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        ILogger<RecommendationService> logger,
        IRecommendationRepository recommendationRepository,
        IDbContextFactory<MediaContext> contextFactory,
        IMemoryCache cache,
        IMovieMetadataProvider movieMetadataProvider,
        ITvShowMetadataProvider tvShowMetadataProvider
    )
    {
        _logger = logger;
        _recommendationRepository = recommendationRepository;
        _contextFactory = contextFactory;
        _cache = cache;
        _movieMetadataProvider = movieMetadataProvider;
        _tvShowMetadataProvider = tvShowMetadataProvider;
    }

    public async Task<List<RecommendationDto>> GetPersonalizedRecommendationsAsync(
        Guid userId,
        string mediaTypeFilter,
        int take = 50,
        CancellationToken ct = default
    )
    {
        bool wantMovie = mediaTypeFilter == MediaTypes.MovieMediaType;
        bool wantTv = mediaTypeFilter == MediaTypes.TvMediaType;
        bool wantAnime = mediaTypeFilter == MediaTypes.AnimeMediaType;

        // Phase 1: Parallel queries — only fetch candidates for the requested type
        Task<List<RecommendationCandidateDto>> movieRecsTask = wantMovie
            ? Task.Run(
                function: async () =>
                {
                    return await _recommendationRepository.GetUnownedMovieRecommendationsAsync(
                        userId: userId,
                        ct: ct
                    );
                },
                cancellationToken: ct
            )
            : Task.FromResult(result: new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> tvRecsTask = wantTv
            ? Task.Run(
                function: async () =>
                {
                    return await _recommendationRepository.GetUnownedTvRecommendationsAsync(
                        userId: userId,
                        ct: ct
                    );
                },
                cancellationToken: ct
            )
            : Task.FromResult(result: new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> animeRecsTask = wantAnime
            ? Task.Run(
                function: async () =>
                {
                    return await _recommendationRepository.GetUnownedAnimeRecommendationsAsync(
                        userId: userId,
                        ct: ct
                    );
                },
                cancellationToken: ct
            )
            : Task.FromResult(result: new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> movieSimTask = wantMovie
            ? Task.Run(
                function: async () =>
                {
                    return await _recommendationRepository.GetUnownedMovieSimilarAsync(userId: userId, ct: ct);
                },
                cancellationToken: ct
            )
            : Task.FromResult(result: new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> tvSimTask = wantTv
            ? Task.Run(
                function: async () =>
                {
                    return await _recommendationRepository.GetUnownedTvSimilarAsync(userId: userId, ct: ct);
                },
                cancellationToken: ct
            )
            : Task.FromResult(result: new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> animeSimTask = wantAnime
            ? Task.Run(
                function: async () =>
                {
                    return await _recommendationRepository.GetUnownedAnimeSimilarAsync(userId: userId, ct: ct);
                },
                cancellationToken: ct
            )
            : Task.FromResult(result: new List<RecommendationCandidateDto>());
        Task<UserAffinityProfile> affinityTask = GetOrBuildAffinityProfileAsync(userId: userId, ct: ct);

        await Task.WhenAll(tasks: [movieRecsTask, tvRecsTask, animeRecsTask, movieSimTask, tvSimTask, animeSimTask, affinityTask]
        );

        _logger.LogDebug(
            message: "Recommendations [{MediaTypeFilter}]: recs={Count}, similar={Count2}, affinity sources={Count3}", args: [mediaTypeFilter, animeRecsTask.Result.Count + movieRecsTask.Result.Count + tvRecsTask.Result.Count, animeSimTask.Result.Count + movieSimTask.Result.Count + tvSimTask.Result.Count, affinityTask.Result.SourceItems.Count]
        );

        UserAffinityProfile profile = affinityTask.Result;

        // Phase 1b: Cross-type keyword candidates — extract keyword maps from high-signal sources
        Dictionary<int, List<int>> movieKeywordMap = new();
        Dictionary<int, List<int>> tvKeywordMap = new();
        Dictionary<int, List<int>> animeKeywordMap = new();

        foreach (KeyValuePair<int, UserAffinitySourceDto> kv in profile.SourceItems)
        {
            UserAffinitySourceDto src = kv.Value;
            if (src.KeywordIds.Count == 0)
                continue;

            bool isHighSignal =
                src.IsFavorited
                || src.Rating is >= 6
                || (
                    src is { TimeWatched: > 0, Duration: > 0 }
                    && (double)src.TimeWatched / src.Duration.Value > 0.5
                );
            if (!isHighSignal)
                continue;

            if (src.MediaType == MediaTypes.MovieMediaType)
                movieKeywordMap[key: src.ItemId] = src.KeywordIds;
            else if (src.MediaType == MediaTypes.AnimeMediaType)
                animeKeywordMap[key: src.ItemId] = src.KeywordIds;
            else
                tvKeywordMap[key: src.ItemId] = src.KeywordIds;
        }

        // Cross-type: use keywords from one type to find candidates in another
        // Anime uses its own keywords to find anime candidates via the TV keyword path (anime is stored as TV)
        Dictionary<int, List<int>> nonMovieKeywordMap = tvKeywordMap
            .Concat(second: animeKeywordMap)
            .ToDictionary(keySelector: kv => kv.Key, elementSelector: kv => kv.Value);

        Task<List<RecommendationCandidateDto>> crossTypeTvTask =
            wantTv && movieKeywordMap.Count > 0
                ? Task.Run(
                    function: async () =>
                    {
                        return await _recommendationRepository.GetKeywordCrossTypeTvCandidatesAsync(
                            userId: userId,
                            movieKeywordMap: movieKeywordMap,
                            minSharedKeywords: 3,
                            maxCandidates: 100,
                            ct: ct
                        );
                    },
                    cancellationToken: ct
                )
                : Task.FromResult(result: new List<RecommendationCandidateDto>());

        Task<List<RecommendationCandidateDto>> crossTypeMovieTask =
            wantMovie && nonMovieKeywordMap.Count > 0
                ? Task.Run(
                    function: async () =>
                    {
                        return await _recommendationRepository.GetKeywordCrossTypeMovieCandidatesAsync(
                            userId: userId,
                            tvKeywordMap: nonMovieKeywordMap,
                            minSharedKeywords: 3,
                            maxCandidates: 100,
                            ct: ct
                        );
                    },
                    cancellationToken: ct
                )
                : Task.FromResult(result: new List<RecommendationCandidateDto>());

        Task<List<RecommendationCandidateDto>> crossTypeAnimeTask =
            wantAnime && movieKeywordMap.Count > 0
                ? Task.Run(
                    function: async () =>
                    {
                        return await _recommendationRepository.GetKeywordCrossTypeAnimeCandidatesAsync(
                            userId: userId,
                            movieKeywordMap: movieKeywordMap,
                            minSharedKeywords: 3,
                            maxCandidates: 100,
                            ct: ct
                        );
                    },
                    cancellationToken: ct
                )
                : Task.FromResult(result: new List<RecommendationCandidateDto>());

        await Task.WhenAll(tasks: [crossTypeTvTask, crossTypeMovieTask, crossTypeAnimeTask]);

        // Phase 2: Merge candidates (same MediaId+MediaType from Recommendation + Similar + Keywords = higher frequency)
        List<RecommendationCandidateDto> allCandidates = MergeCandidates(candidateLists: [movieRecsTask.Result, tvRecsTask.Result, animeRecsTask.Result, movieSimTask.Result, tvSimTask.Result, animeSimTask.Result, crossTypeTvTask.Result, crossTypeMovieTask.Result, crossTypeAnimeTask.Result]
        );

        // Phase 3: Get genre maps for source items — use actual source type from profile, not candidate type
        HashSet<int> allSourceIds = allCandidates.SelectMany(selector: c => c.SourceIds).ToHashSet();
        List<int> allSourceMovieIds = allSourceIds
            .Where(predicate: id =>
                profile.SourceItems.TryGetValue(key: id, value: out UserAffinitySourceDto? s)
                && s.MediaType == MediaTypes.MovieMediaType
            )
            .ToList();
        List<int> allSourceTvIds = allSourceIds
            .Where(predicate: id =>
                profile.SourceItems.TryGetValue(key: id, value: out UserAffinitySourceDto? s)
                && s.MediaType != MediaTypes.MovieMediaType
            )
            .ToList();

        Task<Dictionary<int, List<int>>> movieGenreMapTask = Task.Run(
            function: async () =>
            {
                return await _recommendationRepository.GetGenresForMovieIdsAsync(
                    movieIds: allSourceMovieIds,
                    ct: ct
                );
            },
            cancellationToken: ct
        );
        Task<Dictionary<int, List<int>>> tvGenreMapTask = Task.Run(
            function: async () =>
            {
                return await _recommendationRepository.GetGenresForTvIdsAsync(tvIds: allSourceTvIds, ct: ct);
            },
            cancellationToken: ct
        );

        await Task.WhenAll(tasks: [movieGenreMapTask, tvGenreMapTask]);

        Dictionary<int, List<int>> combinedGenreMap = new(dictionary: movieGenreMapTask.Result);
        foreach (KeyValuePair<int, List<int>> kv in tvGenreMapTask.Result)
            combinedGenreMap[key: kv.Key] = kv.Value;

        // Phase 4: Score all candidates
        List<RecommendationDto> scored = allCandidates
            .Select(selector: c => new RecommendationDto
            {
                Id = c.MediaId,
                Title = c.Title,
                TitleSort = c.TitleSort,
                Overview = c.Overview,
                Poster = c.Poster,
                Backdrop = c.Backdrop,
                ColorPalette = ColorPalette.FromJsonOrNull(json: c.ColorPalette),
                Type = c.MediaType,
                Score = ScoreCandidate(candidate: c, profile: profile, sourceGenreMap: combinedGenreMap),
                SourceCount = c.SourceCount,
                SourceIds = c.SourceIds,
            })
            .Where(predicate: s => s.Poster != null)
            .ToList();

        // Deduplicate by Id — same TMDB ID may appear as both tv and anime; keep highest-scored
        List<RecommendationDto> deduped = scored
            .GroupBy(keySelector: s => s.Id)
            .Select(selector: g => g.OrderByDescending(keySelector: s => s.Score).First())
            .ToList();

        _logger.LogDebug(
            message: "Recommendations [{MediaTypeFilter}]: merged={Count}, scored={Count2}, deduped={Count3}", args: [mediaTypeFilter, allCandidates.Count, scored.Count, deduped.Count]
        );

        // Phase 5: Diversity selection — guarantee floor representation per media type
        return SelectWithDiversity(scored: deduped, take: take);
    }

    public async Task<List<RecommendationDto>> GetHomeRecommendationCarouselAsync(
        Guid userId,
        string mediaTypeFilter,
        int take = 36,
        CancellationToken ct = default
    )
    {
        return await GetPersonalizedRecommendationsAsync(userId: userId, mediaTypeFilter: mediaTypeFilter, take: take, ct: ct);
    }

    public async Task<RecommendationDetailDto?> GetRecommendationDetailAsync(
        Guid userId,
        int mediaId,
        string mediaType,
        string country,
        string language,
        CancellationToken ct = default
    )
    {
        bool isMovie = mediaType == "movie";
        string tmdbLanguage = $"{language}-{country}";

        // Fetch TMDB data and local source items in parallel
        Task<TmdbMovieAppends?> movieAppendsTask = isMovie
            ? _movieMetadataProvider.GetMovieAsync(id: mediaId, language: tmdbLanguage, ct: ct)
            : Task.FromResult<TmdbMovieAppends?>(result: null);
        Task<TmdbTvShowAppends?> tvAppendsTask = !isMovie
            ? _tvShowMetadataProvider.GetTvShowAsync(id: mediaId, language: tmdbLanguage, ct: ct)
            : Task.FromResult<TmdbTvShowAppends?>(result: null);

        Task<(List<Movie> Movies, string? ColorPalette)> sourceMoviesTask = isMovie
            ? _recommendationRepository.GetSourceMoviesForMediaAsync(userId: userId, mediaId: mediaId, ct: ct)
            : Task.FromResult<(List<Movie>, string?)>(result: ([], null));
        Task<(List<Tv> TvShows, string? ColorPalette)> sourceTvsTask = !isMovie
            ? _recommendationRepository.GetSourceTvShowsForMediaAsync(userId: userId, mediaId: mediaId, ct: ct)
            : Task.FromResult<(List<Tv>, string?)>(result: ([], null));

        await Task.WhenAll(tasks: [movieAppendsTask, tvAppendsTask, sourceMoviesTask, sourceTvsTask]);

        // Keyword-based source enrichment: same-type (exclude already-found Rec/Similar sources) + cross-type
        HashSet<int> existingMovieSourceIds = sourceMoviesTask
            .Result.Movies.Select(selector: m => m.Id)
            .ToHashSet();
        HashSet<int> existingTvSourceIds = sourceTvsTask
            .Result.TvShows.Select(selector: t => t.Id)
            .ToHashSet();

        List<Movie> keywordMovieSources = isMovie
            ? await _recommendationRepository.GetKeywordMovieSourcesForMovieAsync(
                userId: userId,
                movieId: mediaId,
                excludeIds: existingMovieSourceIds,
                ct: ct
            )
            : await _recommendationRepository.GetCrossTypeMovieSourcesForTvAsync(
                userId: userId,
                tvId: mediaId,
                ct: ct
            );
        List<Tv> keywordTvSources = !isMovie
            ? await _recommendationRepository.GetKeywordTvSourcesForTvAsync(
                userId: userId,
                tvId: mediaId,
                excludeIds: existingTvSourceIds,
                ct: ct
            )
            : await _recommendationRepository.GetCrossTypeTvSourcesForMovieAsync(
                userId: userId,
                movieId: mediaId,
                ct: ct
            );

        string? rawPalette = isMovie
            ? sourceMoviesTask.Result.ColorPalette
            : sourceTvsTask.Result.ColorPalette;
        ColorPalette? colorPalette = ColorPalette.FromJsonOrNull(json: rawPalette);

        if (isMovie)
        {
            TmdbMovieAppends? appends = movieAppendsTask.Result;
            if (appends is null)
                return null;

            List<RecommendationDetailSourceDto> becauseYouHave = sourceMoviesTask
                .Result.Movies.Select(selector: m => new RecommendationDetailSourceDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    TitleSort = m.TitleSort,
                    Poster = m.Poster,
                    Backdrop = m.Backdrop,
                    Logo = m.Images.FirstOrDefault()?.FilePath,
                    Overview = m.Overview,
                    Year = m.ReleaseDate?.Year,
                    ColorPalette = m.ColorPalette,
                    MediaType = "movie",
                    HaveItems = m.VideoFiles.Count(predicate: vf => vf.Folder != null),
                    NumberOfItems = 1,
                    Duration = m.Runtime ?? 0,
                    Tags = m.KeywordMovies.Select(selector: km => km.Keyword.Name),
                })
                .ToList();

            // Append same-type keyword sources (e.g., Ice Age movies for an Ice Age spinoff)
            becauseYouHave.AddRange(
                collection: keywordMovieSources.Select(selector: m => new RecommendationDetailSourceDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    TitleSort = m.TitleSort,
                    Poster = m.Poster,
                    Backdrop = m.Backdrop,
                    Logo = m.Images.FirstOrDefault()?.FilePath,
                    Overview = m.Overview,
                    Year = m.ReleaseDate?.Year,
                    ColorPalette = m.ColorPalette,
                    MediaType = "movie",
                    HaveItems = m.VideoFiles.Count(predicate: vf => vf.Folder != null),
                    NumberOfItems = 1,
                    Duration = m.Runtime ?? 0,
                    Tags = m.KeywordMovies.Select(selector: km => km.Keyword.Name),
                })
            );

            // Append cross-type TV sources found via keyword overlap
            becauseYouHave.AddRange(
                collection: keywordTvSources.Select(selector: t => new RecommendationDetailSourceDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleSort = t.TitleSort,
                    Poster = t.Poster,
                    Backdrop = t.Backdrop,
                    Logo = t.Images.FirstOrDefault()?.FilePath,
                    Overview = t.Overview,
                    Year = t.FirstAirDate?.Year,
                    ColorPalette = t.ColorPalette,
                    MediaType = "tv",
                    HaveItems = t.Episodes.Count(predicate: e =>
                        e.SeasonNumber > 0 && e.VideoFiles.Any(predicate: vf => vf.Folder != null)
                    ),
                    NumberOfItems = t.Episodes.Count(predicate: e => e.SeasonNumber > 0),
                    Duration = t.Duration ?? 0,
                    Tags = t.KeywordTvs.Select(selector: kt => kt.Keyword.Name),
                })
            );

            // Deduplicate by source family — cap items per title family
            becauseYouHave = DeduplicateSourcesByFamily(sources: becauseYouHave);

            return new()
            {
                Id = appends.Id,
                Title = appends.Title,
                Overview = appends.Overview,
                Poster = appends.PosterPath,
                Backdrop = appends.BackdropPath,
                Logo = appends
                    .Images.Logos.Where(predicate: l => l.Iso6391 == "en")
                    .OrderByDescending(keySelector: l => l.VoteAverage)
                    .FirstOrDefault()
                    ?.FilePath,
                ColorPalette = colorPalette,
                MediaType = "movie",
                Year = appends.ReleaseDate?.Year,
                VoteAverage = appends.VoteAverage,
                Genres = appends.Genres.Select(selector: g => new GenreDto(tmdbGenreMovie: g)),
                ContentRatings = appends
                    .ReleaseDates.Results.Where(predicate: r => r.Iso31661 == country)
                    .SelectMany(selector: r => r.ReleaseDates)
                    .Where(predicate: rd => !string.IsNullOrEmpty(value: rd.Certification))
                    .Select(selector: rd => new ContentRating
                    {
                        Rating = rd.Certification,
                        Iso31661 = country,
                    })
                    .DistinctBy(keySelector: cr => cr.Rating),
                ExternalIds = new() { ImdbId = appends.ExternalIds.ImdbId },
                BecauseYouHave = becauseYouHave,
            };
        }
        else
        {
            TmdbTvShowAppends? appends = tvAppendsTask.Result;
            if (appends is null)
                return null;

            List<RecommendationDetailSourceDto> becauseYouHave = sourceTvsTask
                .Result.TvShows.Select(selector: t => new RecommendationDetailSourceDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleSort = t.TitleSort,
                    Poster = t.Poster,
                    Backdrop = t.Backdrop,
                    Logo = t.Images.FirstOrDefault()?.FilePath,
                    Overview = t.Overview,
                    Year = t.FirstAirDate?.Year,
                    ColorPalette = t.ColorPalette,
                    MediaType = "tv",
                    HaveItems = t.Episodes.Count(predicate: e =>
                        e.SeasonNumber > 0 && e.VideoFiles.Any(predicate: vf => vf.Folder != null)
                    ),
                    NumberOfItems = t.Episodes.Count(predicate: e => e.SeasonNumber > 0),
                    Duration = t.Duration ?? 0,
                    Tags = t.KeywordTvs.Select(selector: kt => kt.Keyword.Name),
                })
                .ToList();

            // Append same-type keyword sources
            becauseYouHave.AddRange(
                collection: keywordTvSources.Select(selector: t => new RecommendationDetailSourceDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleSort = t.TitleSort,
                    Poster = t.Poster,
                    Backdrop = t.Backdrop,
                    Logo = t.Images.FirstOrDefault()?.FilePath,
                    Overview = t.Overview,
                    Year = t.FirstAirDate?.Year,
                    ColorPalette = t.ColorPalette,
                    MediaType = "tv",
                    HaveItems = t.Episodes.Count(predicate: e =>
                        e.SeasonNumber > 0 && e.VideoFiles.Any(predicate: vf => vf.Folder != null)
                    ),
                    NumberOfItems = t.Episodes.Count(predicate: e => e.SeasonNumber > 0),
                    Duration = t.Duration ?? 0,
                    Tags = t.KeywordTvs.Select(selector: kt => kt.Keyword.Name),
                })
            );

            // Append cross-type movie sources found via keyword overlap
            becauseYouHave.AddRange(
                collection: keywordMovieSources.Select(selector: m => new RecommendationDetailSourceDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    TitleSort = m.TitleSort,
                    Poster = m.Poster,
                    Backdrop = m.Backdrop,
                    Logo = m.Images.FirstOrDefault()?.FilePath,
                    Overview = m.Overview,
                    Year = m.ReleaseDate?.Year,
                    ColorPalette = m.ColorPalette,
                    MediaType = "movie",
                    HaveItems = m.VideoFiles.Count(predicate: vf => vf.Folder != null),
                    NumberOfItems = 1,
                    Duration = m.Runtime ?? 0,
                    Tags = m.KeywordMovies.Select(selector: km => km.Keyword.Name),
                })
            );

            // Deduplicate by source family — cap items per title family
            becauseYouHave = DeduplicateSourcesByFamily(sources: becauseYouHave);

            return new()
            {
                Id = appends.Id,
                Title = appends.Name,
                Overview = appends.Overview,
                Poster = appends.PosterPath,
                Backdrop = appends.BackdropPath,
                Logo = appends
                    .Images.Logos.Where(predicate: l => l.Iso6391 == "en")
                    .OrderByDescending(keySelector: l => l.VoteAverage)
                    .FirstOrDefault()
                    ?.FilePath,
                ColorPalette = colorPalette,
                MediaType = "tv",
                Year = appends.FirstAirDate?.Year,
                VoteAverage = appends.VoteAverage,
                Genres = appends.Genres.Select(selector: g => new GenreDto(tmdbGenreMovie: g)),
                ContentRatings = appends
                    .ContentRatings.Results.Where(predicate: cr => cr.Iso31661 == country)
                    .Select(selector: cr => new ContentRating { Rating = cr.Rating, Iso31661 = cr.Iso31661 }),
                ExternalIds = new()
                {
                    ImdbId = appends.ExternalIds.ImdbId,
                    TvdbId = appends.ExternalIds.TvdbId,
                },
                BecauseYouHave = becauseYouHave,
            };
        }
    }

    private static List<RecommendationCandidateDto> MergeCandidates(
        params List<RecommendationCandidateDto>[] candidateLists
    )
    {
        Dictionary<string, RecommendationCandidateDto> merged = new();

        foreach (List<RecommendationCandidateDto> list in candidateLists)
        {
            foreach (RecommendationCandidateDto candidate in list)
            {
                string key = $"{candidate.MediaType}:{candidate.MediaId}";
                if (merged.TryGetValue(key: key, value: out RecommendationCandidateDto? existing))
                {
                    existing.SourceCount += candidate.SourceCount;
                    existing.SourceIds = existing.SourceIds.Union(second: candidate.SourceIds).ToList();
                }
                else
                {
                    merged[key: key] = candidate;
                }
            }
        }

        return merged.Values.ToList();
    }

    private static double ScoreCandidate(
        RecommendationCandidateDto candidate,
        UserAffinityProfile profile,
        Dictionary<int, List<int>> sourceGenreMap
    )
    {
        double score = 0.0;

        // 1. Frequency: use distinct source families instead of raw count to prevent franchise flooding
        //    (e.g., 10 "Tom and Jerry" movies should count as ~1 family, not 10 separate signals)
        int effectiveSourceCount = CountDistinctSourceFamilies(sourceIds: candidate.SourceIds, profile: profile);
        score += Math.Min(val1: effectiveSourceCount, val2: 5) / 5.0 * 3.0;

        // 2. Source rating: average user rating of source items
        List<double> sourceRatings = candidate
            .SourceIds.Where(predicate: id =>
                profile.SourceItems.ContainsKey(key: id) && profile.SourceItems[key: id].Rating.HasValue
            )
            .Select(selector: id => (double)profile.SourceItems[key: id].Rating!.Value)
            .ToList();
        if (sourceRatings.Count > 0)
            score += sourceRatings.Average() / 10.0 * 2.0;

        // 3. Source watch completion
        List<double> completions = candidate
            .SourceIds.Where(predicate: id => profile.SourceItems.ContainsKey(key: id))
            .Select(selector: id =>
            {
                UserAffinitySourceDto src = profile.SourceItems[key: id];
                if (src is { TimeWatched: > 0, Duration: > 0 })
                    return Math.Min(val1: (double)src.TimeWatched / src.Duration.Value, val2: 1.0);
                return 0.0;
            })
            .ToList();
        if (completions.Count > 0)
            score += completions.Average() * 1.5;

        // 4. Genre match via source items' genres as proxy
        List<int> candidateGenreIds = candidate
            .SourceIds.Where(predicate: id => sourceGenreMap.ContainsKey(key: id))
            .SelectMany(selector: id => sourceGenreMap[key: id])
            .Distinct()
            .ToList();
        if (candidateGenreIds.Count > 0)
        {
            double genreMatch = candidateGenreIds
                .Where(predicate: gId => profile.GenreAffinity.ContainsKey(key: gId))
                .Sum(selector: gId => profile.GenreAffinity[key: gId]);
            score += genreMatch / candidateGenreIds.Count * 2.5;
        }

        // 5. Favorite source bonus — check both sets to handle cross-type candidates
        bool hasFavoritedSource = candidate.SourceIds.Any(predicate: id =>
            profile.FavoritedMovieIds.Contains(item: id) || profile.FavoritedTvIds.Contains(item: id)
        );
        if (hasFavoritedSource)
            score += 1.0;

        return score;
    }

    /// <summary>
    /// Limits because_you_have items to max 3 per title family.
    /// Prevents 18 Tom and Jerry items from drowning out more relevant sources like Ice Age movies.
    /// </summary>
    private static List<RecommendationDetailSourceDto> DeduplicateSourcesByFamily(
        List<RecommendationDetailSourceDto> sources,
        int maxPerFamily = 3
    )
    {
        if (sources.Count <= maxPerFamily)
            return sources;

        List<(string Family, RecommendationDetailSourceDto Source)> tagged = [];
        List<string> families = [];

        foreach (RecommendationDetailSourceDto source in sources)
        {
            string title = source.Title.OrEmpty();
            string? matchedFamily = null;

            foreach (string family in families)
            {
                int prefixLen = CommonPrefixLength(a: title, b: family);
                int minLen = Math.Min(val1: title.Length, val2: family.Length);
                if (minLen > 0 && prefixLen >= minLen * 0.6)
                {
                    matchedFamily = family;
                    break;
                }
            }

            if (matchedFamily is null)
            {
                matchedFamily = title;
                families.Add(item: title);
            }

            tagged.Add(item: (matchedFamily, source));
        }

        // Take up to maxPerFamily items from each family, then flatten
        return tagged
            .GroupBy(keySelector: t => t.Family)
            .SelectMany(selector: g => g.Take(count: maxPerFamily).Select(selector: t => t.Source))
            .ToList();
    }

    /// <summary>
    /// Clusters source items by title family to prevent franchise flooding.
    /// Sources sharing a long common prefix (e.g., "Tom and Jerry: X", "Tom and Jerry: Y")
    /// are counted as one family instead of inflating the frequency score.
    /// </summary>
    private static int CountDistinctSourceFamilies(List<int> sourceIds, UserAffinityProfile profile)
    {
        List<string> titles = sourceIds
            .Where(predicate: id => profile.SourceItems.ContainsKey(key: id))
            .Select(selector: id => profile.SourceItems[key: id].Title)
            .Where(predicate: t => !string.IsNullOrEmpty(value: t))
            .ToList();

        if (titles.Count <= 1)
            return titles.Count;

        // Cluster by shared prefix: if two titles share the first 60%+ characters of the shorter one,
        // they're in the same family (e.g., "Tom and Jerry: The Movie" and "Tom and Jerry: Willy Wonka")
        List<string> families = [];
        foreach (string title in titles)
        {
            bool matched = false;
            foreach (string family in families)
            {
                int prefixLen = CommonPrefixLength(a: title, b: family);
                int minLen = Math.Min(val1: title.Length, val2: family.Length);
                if (minLen > 0 && prefixLen >= minLen * 0.6)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                families.Add(item: title);
        }

        return families.Count;
    }

    private static int CommonPrefixLength(string a, string b)
    {
        int len = Math.Min(val1: a.Length, val2: b.Length);
        for (int i = 0; i < len; i++)
        {
            if (char.ToLowerInvariant(c: a[index: i]) != char.ToLowerInvariant(c: b[index: i]))
                return i;
        }
        return len;
    }

    private async Task<UserAffinityProfile> GetOrBuildAffinityProfileAsync(
        Guid userId,
        CancellationToken ct
    )
    {
        string cacheKey = $"reco:affinity:{userId}";

        if (_cache.TryGetValue(key: cacheKey, value: out UserAffinityProfile? cached) && cached is not null)
            return cached;

        Task<List<UserAffinitySourceDto>> movieAffinityTask = Task.Run(
            function: async () =>
            {
                return await _recommendationRepository.GetUserMovieAffinityDataAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );
        Task<List<UserAffinitySourceDto>> tvAffinityTask = Task.Run(
            function: async () =>
            {
                return await _recommendationRepository.GetUserTvAffinityDataAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );
        Task<List<UserAffinitySourceDto>> animeAffinityTask = Task.Run(
            function: async () =>
            {
                return await _recommendationRepository.GetUserAnimeAffinityDataAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );

        await Task.WhenAll(tasks: [movieAffinityTask, tvAffinityTask, animeAffinityTask]);

        List<UserAffinitySourceDto> allSources = movieAffinityTask
            .Result.Concat(second: tvAffinityTask.Result)
            .Concat(second: animeAffinityTask.Result)
            .ToList();

        Dictionary<int, double> genreScores = new();
        Dictionary<int, UserAffinitySourceDto> sourceMap = new();
        HashSet<int> favMovies = [];
        HashSet<int> favTvs = [];

        foreach (UserAffinitySourceDto src in allSources)
        {
            sourceMap[key: src.ItemId] = src;
            if (src.IsFavorited)
            {
                if (src.MediaType == MediaTypes.MovieMediaType)
                    favMovies.Add(item: src.ItemId);
                else
                    favTvs.Add(item: src.ItemId);
            }

            double weight = 1.0;
            if (src.Rating.HasValue)
                weight += (src.Rating.Value - 5) / 5.0;
            if (
                src is { TimeWatched: > 0, Duration: > 0 }
                && (double)src.TimeWatched / src.Duration.Value > 0.8
            )
                weight += 0.5;
            if (src.IsFavorited)
                weight += 1.0;

            foreach (int genreId in src.GenreIds)
            {
                genreScores.TryAdd(key: genreId, value: 0);
                genreScores[key: genreId] += weight;
            }
        }

        // Normalize genre scores to 0–1 range
        double maxGenre = genreScores.Values.DefaultIfEmpty(defaultValue: 1).Max();
        Dictionary<int, double> genreAffinity = genreScores.ToDictionary(
            keySelector: kv => kv.Key,
            elementSelector: kv => kv.Value / maxGenre
        );

        UserAffinityProfile profile = new()
        {
            GenreAffinity = genreAffinity,
            SourceItems = sourceMap,
            FavoritedMovieIds = favMovies,
            FavoritedTvIds = favTvs,
        };

        MemoryCacheEntryOptions cacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(minutes: 10),
            Size = 1,
        };
        _cache.Set(key: cacheKey, value: profile, options: cacheOptions);

        return profile;
    }

    /// <summary>
    /// Guarantees a minimum floor of (take / typeCount) results per media type,
    /// then fills remaining slots with the highest-scored items from any type.
    /// </summary>
    private static List<RecommendationDto> SelectWithDiversity(
        List<RecommendationDto> scored,
        int take
    )
    {
        Dictionary<string, Queue<RecommendationDto>> byType = scored
            .GroupBy(keySelector: s => s.Type)
            .ToDictionary(
                keySelector: g => g.Key,
                elementSelector: g => new Queue<RecommendationDto>(collection: g.OrderByDescending(keySelector: s => s.Score))
            );

        int typeCount = byType.Count;
        if (typeCount <= 1)
            return scored.OrderByDescending(keySelector: s => s.Score).Take(count: take).ToList();

        // Give each type a guaranteed floor of (take / typeCount) slots
        int floorSlots = take / typeCount;
        List<RecommendationDto> result = [];
        foreach (Queue<RecommendationDto> queue in byType.Values)
        {
            int toTake = Math.Min(val1: floorSlots, val2: queue.Count);
            for (int i = 0; i < toTake; i++)
                result.Add(item: queue.Dequeue());
        }

        // Fill remaining slots with best-scored items from any type
        int remaining = take - result.Count;
        if (remaining > 0)
        {
            List<RecommendationDto> overflow = byType
                .Values.SelectMany(selector: q => q)
                .OrderByDescending(keySelector: s => s.Score)
                .Take(count: remaining)
                .ToList();
            result.AddRange(collection: overflow);
        }

        return result.OrderByDescending(keySelector: s => s.Score).ToList();
    }

    internal record UserAffinityProfile
    {
        public Dictionary<int, double> GenreAffinity { get; init; } = new();
        public Dictionary<int, UserAffinitySourceDto> SourceItems { get; init; } = new();
        public HashSet<int> FavoritedMovieIds { get; init; } = [];
        public HashSet<int> FavoritedTvIds { get; init; } = [];
    }
}
