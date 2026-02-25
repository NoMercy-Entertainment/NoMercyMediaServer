using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.Api.Services;

public class RecommendationService
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly RecommendationRepository _recommendationRepository;
    private readonly IMemoryCache _cache;

    public RecommendationService(
        RecommendationRepository recommendationRepository,
        IDbContextFactory<MediaContext> contextFactory,
        IMemoryCache cache)
    {
        _recommendationRepository = recommendationRepository;
        _contextFactory = contextFactory;
        _cache = cache;
    }

    public async Task<List<RecommendationDto>> GetPersonalizedRecommendationsAsync(
        Guid userId, string mediaTypeFilter, int take = 50, CancellationToken ct = default)
    {
        bool wantMovie = mediaTypeFilter == Config.MovieMediaType;
        bool wantTv = mediaTypeFilter == Config.TvMediaType;
        bool wantAnime = mediaTypeFilter == Config.AnimeMediaType;

        // Phase 1: Parallel queries — only fetch candidates for the requested type
        Task<List<RecommendationCandidateDto>> movieRecsTask = wantMovie
            ? Task.Run(async () =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetUnownedMovieRecommendationsAsync(context, userId, ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> tvRecsTask = wantTv
            ? Task.Run(async () =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetUnownedTvRecommendationsAsync(context, userId, ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> animeRecsTask = wantAnime
            ? Task.Run(async () =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetUnownedAnimeRecommendationsAsync(context, userId, ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> movieSimTask = wantMovie
            ? Task.Run(async () =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetUnownedMovieSimilarAsync(context, userId, ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> tvSimTask = wantTv
            ? Task.Run(async () =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetUnownedTvSimilarAsync(context, userId, ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());
        Task<List<RecommendationCandidateDto>> animeSimTask = wantAnime
            ? Task.Run(async () =>
            {
                await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetUnownedAnimeSimilarAsync(context, userId, ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());
        Task<UserAffinityProfile> affinityTask = GetOrBuildAffinityProfileAsync(userId, ct);

        await Task.WhenAll(movieRecsTask, tvRecsTask, animeRecsTask, movieSimTask, tvSimTask, animeSimTask, affinityTask);

        Logger.App($"Recommendations [{mediaTypeFilter}]: recs={animeRecsTask.Result.Count + movieRecsTask.Result.Count + tvRecsTask.Result.Count}, " +
                   $"similar={animeSimTask.Result.Count + movieSimTask.Result.Count + tvSimTask.Result.Count}, " +
                   $"affinity sources={affinityTask.Result.SourceItems.Count}",
            LogEventLevel.Debug);

        UserAffinityProfile profile = affinityTask.Result;

        // Phase 1b: Cross-type keyword candidates — extract keyword maps from high-signal sources
        Dictionary<int, List<int>> movieKeywordMap = new();
        Dictionary<int, List<int>> tvKeywordMap = new();
        Dictionary<int, List<int>> animeKeywordMap = new();

        foreach (KeyValuePair<int, UserAffinitySourceDto> kv in profile.SourceItems)
        {
            UserAffinitySourceDto src = kv.Value;
            if (src.KeywordIds.Count == 0) continue;

            bool isHighSignal = src.IsFavorited
                || (src.Rating.HasValue && src.Rating.Value >= 6)
                || (src.TimeWatched is > 0 && src.Duration is > 0
                    && (double)src.TimeWatched / src.Duration.Value > 0.5);
            if (!isHighSignal) continue;

            if (src.MediaType == Config.MovieMediaType)
                movieKeywordMap[src.ItemId] = src.KeywordIds;
            else if (src.MediaType == Config.AnimeMediaType)
                animeKeywordMap[src.ItemId] = src.KeywordIds;
            else
                tvKeywordMap[src.ItemId] = src.KeywordIds;
        }

        // Cross-type: use keywords from one type to find candidates in another
        // Anime uses its own keywords to find anime candidates via the TV keyword path (anime is stored as TV)
        Dictionary<int, List<int>> nonMovieKeywordMap = tvKeywordMap
            .Concat(animeKeywordMap)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Task<List<RecommendationCandidateDto>> crossTypeTvTask = wantTv && movieKeywordMap.Count > 0
            ? Task.Run(async () =>
            {
                await using MediaContext ctx = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetKeywordCrossTypeTvCandidatesAsync(
                    ctx, userId, movieKeywordMap, minSharedKeywords: 3, maxCandidates: 100, ct: ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());

        Task<List<RecommendationCandidateDto>> crossTypeMovieTask = wantMovie && nonMovieKeywordMap.Count > 0
            ? Task.Run(async () =>
            {
                await using MediaContext ctx = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetKeywordCrossTypeMovieCandidatesAsync(
                    ctx, userId, nonMovieKeywordMap, minSharedKeywords: 3, maxCandidates: 100, ct: ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());

        Task<List<RecommendationCandidateDto>> crossTypeAnimeTask = wantAnime && movieKeywordMap.Count > 0
            ? Task.Run(async () =>
            {
                await using MediaContext ctx = await _contextFactory.CreateDbContextAsync(ct);
                return await _recommendationRepository.GetKeywordCrossTypeAnimeCandidatesAsync(
                    ctx, userId, movieKeywordMap, minSharedKeywords: 3, maxCandidates: 100, ct: ct);
            }, ct)
            : Task.FromResult(new List<RecommendationCandidateDto>());

        await Task.WhenAll(crossTypeTvTask, crossTypeMovieTask, crossTypeAnimeTask);

        // Phase 2: Merge candidates (same MediaId+MediaType from Recommendation + Similar + Keywords = higher frequency)
        List<RecommendationCandidateDto> allCandidates = MergeCandidates(
            movieRecsTask.Result, tvRecsTask.Result, animeRecsTask.Result,
            movieSimTask.Result, tvSimTask.Result, animeSimTask.Result,
            crossTypeTvTask.Result, crossTypeMovieTask.Result, crossTypeAnimeTask.Result);

        // Phase 3: Get genre maps for source items — use actual source type from profile, not candidate type
        HashSet<int> allSourceIds = allCandidates.SelectMany(c => c.SourceIds).ToHashSet();
        List<int> allSourceMovieIds = allSourceIds
            .Where(id => profile.SourceItems.TryGetValue(id, out UserAffinitySourceDto? s)
                && s.MediaType == Config.MovieMediaType)
            .ToList();
        List<int> allSourceTvIds = allSourceIds
            .Where(id => profile.SourceItems.TryGetValue(id, out UserAffinitySourceDto? s)
                && s.MediaType != Config.MovieMediaType)
            .ToList();

        Task<Dictionary<int, List<int>>> movieGenreMapTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetGenresForMovieIdsAsync(context, allSourceMovieIds, ct);
        }, ct);
        Task<Dictionary<int, List<int>>> tvGenreMapTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetGenresForTvIdsAsync(context, allSourceTvIds, ct);
        }, ct);

        await Task.WhenAll(movieGenreMapTask, tvGenreMapTask);

        Dictionary<int, List<int>> combinedGenreMap = new(movieGenreMapTask.Result);
        foreach (KeyValuePair<int, List<int>> kv in tvGenreMapTask.Result)
            combinedGenreMap[kv.Key] = kv.Value;

        // Phase 4: Score all candidates
        List<RecommendationDto> scored = allCandidates
            .Select(c => new RecommendationDto
            {
                Id = c.MediaId,
                Title = c.Title,
                TitleSort = c.TitleSort,
                Overview = c.Overview,
                Poster = c.Poster,
                Backdrop = c.Backdrop,
                ColorPalette = !string.IsNullOrEmpty(c.ColorPalette)
                    ? JsonConvert.DeserializeObject<IColorPalettes>(c.ColorPalette)
                    : null,
                Type = c.MediaType,
                Score = ScoreCandidate(c, profile, combinedGenreMap),
                SourceCount = c.SourceCount,
                SourceIds = c.SourceIds
            })
            .Where(s => s.Poster != null)
            .ToList();

        // Deduplicate by Id — same TMDB ID may appear as both tv and anime; keep highest-scored
        List<RecommendationDto> deduped = scored
            .GroupBy(s => s.Id)
            .Select(g => g.OrderByDescending(s => s.Score).First())
            .ToList();

        Logger.App($"Recommendations [{mediaTypeFilter}]: merged={allCandidates.Count}, scored={scored.Count}, deduped={deduped.Count}",
            LogEventLevel.Debug);

        // Phase 5: Diversity selection — guarantee floor representation per media type
        return SelectWithDiversity(deduped, take);
    }

    public async Task<List<RecommendationDto>> GetHomeRecommendationCarouselAsync(
        Guid userId, string mediaTypeFilter, int take = 36, CancellationToken ct = default)
    {
        return await GetPersonalizedRecommendationsAsync(userId, mediaTypeFilter, take, ct);
    }

    public async Task<RecommendationDetailDto?> GetRecommendationDetailAsync(
        Guid userId, int mediaId, string mediaType, string country, string language, CancellationToken ct = default)
    {
        bool isMovie = mediaType == "movie";
        string tmdbLanguage = $"{language}-{country}";

        // Fetch TMDB data and local source items in parallel
        Task<TmdbMovieAppends?> movieAppendsTask = isMovie
            ? new TmdbMovieClient(mediaId, language: tmdbLanguage).WithAllAppends()
            : Task.FromResult<TmdbMovieAppends?>(null);
        Task<TmdbTvShowAppends?> tvAppendsTask = !isMovie
            ? new TmdbTvClient(mediaId, language: tmdbLanguage).WithAllAppends()
            : Task.FromResult<TmdbTvShowAppends?>(null);

        await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);

        Task<(List<Movie> Movies, string? ColorPalette)> sourceMoviesTask = isMovie
            ? _recommendationRepository.GetSourceMoviesForMediaAsync(context, userId, mediaId, ct)
            : Task.FromResult<(List<Movie>, string?)>(([], null));
        Task<(List<Tv> TvShows, string? ColorPalette)> sourceTvsTask = !isMovie
            ? _recommendationRepository.GetSourceTvShowsForMediaAsync(context, userId, mediaId, ct)
            : Task.FromResult<(List<Tv>, string?)>(([], null));

        await Task.WhenAll(movieAppendsTask, tvAppendsTask, sourceMoviesTask, sourceTvsTask);

        // Keyword-based source enrichment: same-type (exclude already-found Rec/Similar sources) + cross-type
        HashSet<int> existingMovieSourceIds = sourceMoviesTask.Result.Movies.Select(m => m.Id).ToHashSet();
        HashSet<int> existingTvSourceIds = sourceTvsTask.Result.TvShows.Select(t => t.Id).ToHashSet();

        List<Movie> keywordMovieSources = isMovie
            ? await _recommendationRepository.GetKeywordMovieSourcesForMovieAsync(context, userId, mediaId, existingMovieSourceIds, ct)
            : await _recommendationRepository.GetCrossTypeMovieSourcesForTvAsync(context, userId, mediaId, ct);
        List<Tv> keywordTvSources = !isMovie
            ? await _recommendationRepository.GetKeywordTvSourcesForTvAsync(context, userId, mediaId, existingTvSourceIds, ct)
            : await _recommendationRepository.GetCrossTypeTvSourcesForMovieAsync(context, userId, mediaId, ct);

        string? rawPalette = isMovie ? sourceMoviesTask.Result.ColorPalette : sourceTvsTask.Result.ColorPalette;
        IColorPalettes? colorPalette = !string.IsNullOrEmpty(rawPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(rawPalette)
            : null;

        if (isMovie)
        {
            TmdbMovieAppends? appends = movieAppendsTask.Result;
            if (appends is null) return null;

            List<RecommendationDetailSourceDto> becauseYouHave = sourceMoviesTask.Result.Movies
                .Select(m => new RecommendationDetailSourceDto
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
                    HaveItems = m.VideoFiles.Count(vf => vf.Folder != null),
                    NumberOfItems = 1,
                    Duration = m.Runtime ?? 0,
                    Tags = m.KeywordMovies.Select(km => km.Keyword.Name)
                }).ToList();

            // Append same-type keyword sources (e.g., Ice Age movies for an Ice Age spinoff)
            becauseYouHave.AddRange(keywordMovieSources.Select(m => new RecommendationDetailSourceDto
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
                HaveItems = m.VideoFiles.Count(vf => vf.Folder != null),
                NumberOfItems = 1,
                Duration = m.Runtime ?? 0,
                Tags = m.KeywordMovies.Select(km => km.Keyword.Name)
            }));

            // Append cross-type TV sources found via keyword overlap
            becauseYouHave.AddRange(keywordTvSources.Select(t => new RecommendationDetailSourceDto
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
                HaveItems = t.Episodes.Count(e => e.SeasonNumber > 0 && e.VideoFiles.Any(vf => vf.Folder != null)),
                NumberOfItems = t.Episodes.Count(e => e.SeasonNumber > 0),
                Duration = t.Duration ?? 0,
                Tags = t.KeywordTvs.Select(kt => kt.Keyword.Name)
            }));

            // Deduplicate by source family — cap items per title family
            becauseYouHave = DeduplicateSourcesByFamily(becauseYouHave);

            return new()
            {
                Id = appends.Id,
                Title = appends.Title,
                Overview = appends.Overview,
                Poster = appends.PosterPath,
                Backdrop = appends.BackdropPath,
                Logo = appends.Images.Logos
                    .Where(l => l.Iso6391 == "en")
                    .OrderByDescending(l => l.VoteAverage)
                    .FirstOrDefault()?.FilePath,
                ColorPalette = colorPalette,
                MediaType = "movie",
                Year = appends.ReleaseDate?.Year,
                VoteAverage = appends.VoteAverage,
                Genres = appends.Genres.Select(g => new GenreDto(g)),
                ContentRatings = appends.ReleaseDates.Results
                    .Where(r => r.Iso31661 == country)
                    .SelectMany(r => r.ReleaseDates)
                    .Where(rd => !string.IsNullOrEmpty(rd.Certification))
                    .Select(rd => new ContentRating
                    {
                        Rating = rd.Certification,
                        Iso31661 = country
                    })
                    .DistinctBy(cr => cr.Rating),
                ExternalIds = new()
                {
                    ImdbId = appends.ExternalIds.ImdbId
                },
                BecauseYouHave = becauseYouHave
            };
        }
        else
        {
            TmdbTvShowAppends? appends = tvAppendsTask.Result;
            if (appends is null) return null;

            List<RecommendationDetailSourceDto> becauseYouHave = sourceTvsTask.Result.TvShows
                .Select(t => new RecommendationDetailSourceDto
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
                    HaveItems = t.Episodes.Count(e => e.SeasonNumber > 0 && e.VideoFiles.Any(vf => vf.Folder != null)),
                    NumberOfItems = t.Episodes.Count(e => e.SeasonNumber > 0),
                    Duration = t.Duration ?? 0,
                    Tags = t.KeywordTvs.Select(kt => kt.Keyword.Name)
                }).ToList();

            // Append same-type keyword sources
            becauseYouHave.AddRange(keywordTvSources.Select(t => new RecommendationDetailSourceDto
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
                HaveItems = t.Episodes.Count(e => e.SeasonNumber > 0 && e.VideoFiles.Any(vf => vf.Folder != null)),
                NumberOfItems = t.Episodes.Count(e => e.SeasonNumber > 0),
                Duration = t.Duration ?? 0,
                Tags = t.KeywordTvs.Select(kt => kt.Keyword.Name)
            }));

            // Append cross-type movie sources found via keyword overlap
            becauseYouHave.AddRange(keywordMovieSources.Select(m => new RecommendationDetailSourceDto
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
                HaveItems = m.VideoFiles.Count(vf => vf.Folder != null),
                NumberOfItems = 1,
                Duration = m.Runtime ?? 0,
                Tags = m.KeywordMovies.Select(km => km.Keyword.Name)
            }));

            // Deduplicate by source family — cap items per title family
            becauseYouHave = DeduplicateSourcesByFamily(becauseYouHave);

            return new()
            {
                Id = appends.Id,
                Title = appends.Name,
                Overview = appends.Overview,
                Poster = appends.PosterPath,
                Backdrop = appends.BackdropPath,
                Logo = appends.Images.Logos
                    .Where(l => l.Iso6391 == "en")
                    .OrderByDescending(l => l.VoteAverage)
                    .FirstOrDefault()?.FilePath,
                ColorPalette = colorPalette,
                MediaType = "tv",
                Year = appends.FirstAirDate?.Year,
                VoteAverage = appends.VoteAverage,
                Genres = appends.Genres.Select(g => new GenreDto(g)),
                ContentRatings = appends.ContentRatings.Results
                    .Where(cr => cr.Iso31661 == country)
                    .Select(cr => new ContentRating
                    {
                        Rating = cr.Rating,
                        Iso31661 = cr.Iso31661
                    }),
                ExternalIds = new()
                {
                    ImdbId = appends.ExternalIds.ImdbId,
                    TvdbId = appends.ExternalIds.TvdbId
                },
                BecauseYouHave = becauseYouHave
            };
        }
    }

    private static List<RecommendationCandidateDto> MergeCandidates(
        params List<RecommendationCandidateDto>[] candidateLists)
    {
        Dictionary<string, RecommendationCandidateDto> merged = new();

        foreach (List<RecommendationCandidateDto> list in candidateLists)
        {
            foreach (RecommendationCandidateDto candidate in list)
            {
                string key = $"{candidate.MediaType}:{candidate.MediaId}";
                if (merged.TryGetValue(key, out RecommendationCandidateDto? existing))
                {
                    existing.SourceCount += candidate.SourceCount;
                    existing.SourceIds = existing.SourceIds.Union(candidate.SourceIds).ToList();
                }
                else
                {
                    merged[key] = candidate;
                }
            }
        }

        return merged.Values.ToList();
    }

    private static double ScoreCandidate(
        RecommendationCandidateDto candidate,
        UserAffinityProfile profile,
        Dictionary<int, List<int>> sourceGenreMap)
    {
        double score = 0.0;

        // 1. Frequency: use distinct source families instead of raw count to prevent franchise flooding
        //    (e.g., 10 "Tom and Jerry" movies should count as ~1 family, not 10 separate signals)
        int effectiveSourceCount = CountDistinctSourceFamilies(candidate.SourceIds, profile);
        score += Math.Min(effectiveSourceCount, 5) / 5.0 * 3.0;

        // 2. Source rating: average user rating of source items
        List<double> sourceRatings = candidate.SourceIds
            .Where(id => profile.SourceItems.ContainsKey(id) && profile.SourceItems[id].Rating.HasValue)
            .Select(id => (double)profile.SourceItems[id].Rating!.Value)
            .ToList();
        if (sourceRatings.Count > 0)
            score += sourceRatings.Average() / 10.0 * 2.0;

        // 3. Source watch completion
        List<double> completions = candidate.SourceIds
            .Where(id => profile.SourceItems.ContainsKey(id))
            .Select(id =>
            {
                UserAffinitySourceDto src = profile.SourceItems[id];
                if (src.TimeWatched is > 0 && src.Duration is > 0)
                    return Math.Min((double)src.TimeWatched / src.Duration.Value, 1.0);
                return 0.0;
            })
            .ToList();
        if (completions.Count > 0)
            score += completions.Average() * 1.5;

        // 4. Genre match via source items' genres as proxy
        List<int> candidateGenreIds = candidate.SourceIds
            .Where(id => sourceGenreMap.ContainsKey(id))
            .SelectMany(id => sourceGenreMap[id])
            .Distinct()
            .ToList();
        if (candidateGenreIds.Count > 0)
        {
            double genreMatch = candidateGenreIds
                .Where(gId => profile.GenreAffinity.ContainsKey(gId))
                .Sum(gId => profile.GenreAffinity[gId]);
            score += genreMatch / candidateGenreIds.Count * 2.5;
        }

        // 5. Favorite source bonus — check both sets to handle cross-type candidates
        bool hasFavoritedSource = candidate.SourceIds.Any(id =>
            profile.FavoritedMovieIds.Contains(id) || profile.FavoritedTvIds.Contains(id));
        if (hasFavoritedSource)
            score += 1.0;

        return score;
    }

    /// <summary>
    /// Limits because_you_have items to max 3 per title family.
    /// Prevents 18 Tom and Jerry items from drowning out more relevant sources like Ice Age movies.
    /// </summary>
    private static List<RecommendationDetailSourceDto> DeduplicateSourcesByFamily(
        List<RecommendationDetailSourceDto> sources, int maxPerFamily = 3)
    {
        if (sources.Count <= maxPerFamily) return sources;

        List<(string Family, RecommendationDetailSourceDto Source)> tagged = [];
        List<string> families = [];

        foreach (RecommendationDetailSourceDto source in sources)
        {
            string title = source.Title ?? string.Empty;
            string? matchedFamily = null;

            foreach (string family in families)
            {
                int prefixLen = CommonPrefixLength(title, family);
                int minLen = Math.Min(title.Length, family.Length);
                if (minLen > 0 && prefixLen >= minLen * 0.6)
                {
                    matchedFamily = family;
                    break;
                }
            }

            if (matchedFamily is null)
            {
                matchedFamily = title;
                families.Add(title);
            }

            tagged.Add((matchedFamily, source));
        }

        // Take up to maxPerFamily items from each family, then flatten
        return tagged
            .GroupBy(t => t.Family)
            .SelectMany(g => g.Take(maxPerFamily).Select(t => t.Source))
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
            .Where(id => profile.SourceItems.ContainsKey(id))
            .Select(id => profile.SourceItems[id].Title)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        if (titles.Count <= 1) return titles.Count;

        // Cluster by shared prefix: if two titles share the first 60%+ characters of the shorter one,
        // they're in the same family (e.g., "Tom and Jerry: The Movie" and "Tom and Jerry: Willy Wonka")
        List<string> families = [];
        foreach (string title in titles)
        {
            bool matched = false;
            foreach (string family in families)
            {
                int prefixLen = CommonPrefixLength(title, family);
                int minLen = Math.Min(title.Length, family.Length);
                if (minLen > 0 && prefixLen >= minLen * 0.6)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                families.Add(title);
        }

        return families.Count;
    }

    private static int CommonPrefixLength(string a, string b)
    {
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            if (char.ToLowerInvariant(a[i]) != char.ToLowerInvariant(b[i]))
                return i;
        }
        return len;
    }

    private async Task<UserAffinityProfile> GetOrBuildAffinityProfileAsync(Guid userId, CancellationToken ct)
    {
        string cacheKey = $"reco:affinity:{userId}";

        if (_cache.TryGetValue(cacheKey, out UserAffinityProfile? cached) && cached is not null)
            return cached;

        Task<List<UserAffinitySourceDto>> movieAffinityTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUserMovieAffinityDataAsync(context, userId, ct);
        }, ct);
        Task<List<UserAffinitySourceDto>> tvAffinityTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUserTvAffinityDataAsync(context, userId, ct);
        }, ct);
        Task<List<UserAffinitySourceDto>> animeAffinityTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUserAnimeAffinityDataAsync(context, userId, ct);
        }, ct);

        await Task.WhenAll(movieAffinityTask, tvAffinityTask, animeAffinityTask);

        List<UserAffinitySourceDto> allSources = movieAffinityTask.Result
            .Concat(tvAffinityTask.Result)
            .Concat(animeAffinityTask.Result).ToList();

        Dictionary<int, double> genreScores = new();
        Dictionary<int, UserAffinitySourceDto> sourceMap = new();
        HashSet<int> favMovies = [];
        HashSet<int> favTvs = [];

        foreach (UserAffinitySourceDto src in allSources)
        {
            sourceMap[src.ItemId] = src;
            if (src.IsFavorited)
            {
                if (src.MediaType == Config.MovieMediaType)
                    favMovies.Add(src.ItemId);
                else
                    favTvs.Add(src.ItemId);
            }

            double weight = 1.0;
            if (src.Rating.HasValue)
                weight += (src.Rating.Value - 5) / 5.0;
            if (src.TimeWatched is > 0 && src.Duration is > 0 &&
                (double)src.TimeWatched / src.Duration.Value > 0.8)
                weight += 0.5;
            if (src.IsFavorited)
                weight += 1.0;

            foreach (int genreId in src.GenreIds)
            {
                genreScores.TryAdd(genreId, 0);
                genreScores[genreId] += weight;
            }
        }

        // Normalize genre scores to 0–1 range
        double maxGenre = genreScores.Values.DefaultIfEmpty(1).Max();
        Dictionary<int, double> genreAffinity = genreScores
            .ToDictionary(kv => kv.Key, kv => kv.Value / maxGenre);

        UserAffinityProfile profile = new()
        {
            GenreAffinity = genreAffinity,
            SourceItems = sourceMap,
            FavoritedMovieIds = favMovies,
            FavoritedTvIds = favTvs
        };

        MemoryCacheEntryOptions cacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(10),
            Size = 1
        };
        _cache.Set(cacheKey, profile, cacheOptions);

        return profile;
    }

    /// <summary>
    /// Guarantees a minimum floor of (take / typeCount) results per media type,
    /// then fills remaining slots with the highest-scored items from any type.
    /// </summary>
    private static List<RecommendationDto> SelectWithDiversity(
        List<RecommendationDto> scored, int take)
    {
        Dictionary<string, Queue<RecommendationDto>> byType = scored
            .GroupBy(s => s.Type)
            .ToDictionary(
                g => g.Key,
                g => new Queue<RecommendationDto>(g.OrderByDescending(s => s.Score)));

        int typeCount = byType.Count;
        if (typeCount <= 1)
            return scored.OrderByDescending(s => s.Score).Take(take).ToList();

        // Give each type a guaranteed floor of (take / typeCount) slots
        int floorSlots = take / typeCount;
        List<RecommendationDto> result = [];
        foreach (Queue<RecommendationDto> queue in byType.Values)
        {
            int toTake = Math.Min(floorSlots, queue.Count);
            for (int i = 0; i < toTake; i++)
                result.Add(queue.Dequeue());
        }

        // Fill remaining slots with best-scored items from any type
        int remaining = take - result.Count;
        if (remaining > 0)
        {
            List<RecommendationDto> overflow = byType.Values
                .SelectMany(q => q)
                .OrderByDescending(s => s.Score)
                .Take(remaining)
                .ToList();
            result.AddRange(overflow);
        }

        return result.OrderByDescending(s => s.Score).ToList();
    }

    internal record UserAffinityProfile
    {
        public Dictionary<int, double> GenreAffinity { get; init; } = new();
        public Dictionary<int, UserAffinitySourceDto> SourceItems { get; init; } = new();
        public HashSet<int> FavoritedMovieIds { get; init; } = [];
        public HashSet<int> FavoritedTvIds { get; init; } = [];
    }
}
