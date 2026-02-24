using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.NmSystem.Information;

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
        Guid userId, int take = 50, CancellationToken ct = default)
    {
        // Phase 1: Parallel queries with separate DbContexts
        Task<List<RecommendationCandidateDto>> movieRecsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUnownedMovieRecommendationsAsync(context, userId, ct);
        }, ct);
        Task<List<RecommendationCandidateDto>> tvRecsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUnownedTvRecommendationsAsync(context, userId, ct);
        }, ct);
        Task<List<RecommendationCandidateDto>> animeRecsTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUnownedAnimeRecommendationsAsync(context, userId, ct);
        }, ct);
        Task<List<RecommendationCandidateDto>> movieSimTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUnownedMovieSimilarAsync(context, userId, ct);
        }, ct);
        Task<List<RecommendationCandidateDto>> tvSimTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUnownedTvSimilarAsync(context, userId, ct);
        }, ct);
        Task<List<RecommendationCandidateDto>> animeSimTask = Task.Run(async () =>
        {
            await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);
            return await _recommendationRepository.GetUnownedAnimeSimilarAsync(context, userId, ct);
        }, ct);
        Task<UserAffinityProfile> affinityTask = GetOrBuildAffinityProfileAsync(userId, ct);

        await Task.WhenAll(movieRecsTask, tvRecsTask, animeRecsTask, movieSimTask, tvSimTask, animeSimTask, affinityTask);

        // Phase 2: Merge candidates (same MediaId+MediaType from Recommendation + Similar = higher frequency)
        List<RecommendationCandidateDto> allCandidates = MergeCandidates(
            movieRecsTask.Result, tvRecsTask.Result, animeRecsTask.Result,
            movieSimTask.Result, tvSimTask.Result, animeSimTask.Result);

        UserAffinityProfile profile = affinityTask.Result;

        // Phase 3: Get genre maps for source items
        // Anime candidates also use TvFromId (same Tv table), so include both tv and anime source IDs
        List<int> allSourceMovieIds = allCandidates
            .Where(c => c.MediaType == Config.MovieMediaType)
            .SelectMany(c => c.SourceIds).Distinct().ToList();
        List<int> allSourceTvIds = allCandidates
            .Where(c => c.MediaType == Config.TvMediaType || c.MediaType == Config.AnimeMediaType)
            .SelectMany(c => c.SourceIds).Distinct().ToList();

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
                BecauseYouHave = c.SourceIds
                    .Where(id => profile.SourceItems.ContainsKey(id))
                    .OrderByDescending(id => profile.SourceItems[id].Rating ?? 0)
                    .ThenByDescending(id => profile.SourceItems[id].TimeWatched ?? 0)
                    .Take(3)
                    .Select(id => new RecommendationSourceDto
                    {
                        Id = id,
                        Title = profile.SourceItems[id].Title,
                        Poster = profile.SourceItems[id].Poster,
                        Type = profile.SourceItems[id].MediaType,
                        ColorPalette = !string.IsNullOrEmpty(profile.SourceItems[id].ColorPalette)
                            ? JsonConvert.DeserializeObject<IColorPalettes>(profile.SourceItems[id].ColorPalette)
                            : null
                    })
                    .ToList()
            })
            .Where(s => s.Poster != null)
            .ToList();

        // Deduplicate by Id — same TMDB ID may appear as both tv and anime; keep highest-scored
        List<RecommendationDto> deduped = scored
            .GroupBy(s => s.Id)
            .Select(g => g.OrderByDescending(s => s.Score).First())
            .ToList();

        // Phase 5: Diversity selection — guarantee floor representation per media type
        return SelectWithDiversity(deduped, take);
    }

    public async Task<List<RecommendationDto>> GetHomeRecommendationCarouselAsync(
        Guid userId, int take = 36, CancellationToken ct = default)
    {
        return await GetPersonalizedRecommendationsAsync(userId, take, ct);
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

        // 1. Frequency: how many distinct owned items recommend this (max 5 for normalization)
        score += Math.Min(candidate.SourceCount, 5) / 5.0 * 3.0;

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

        // 5. Favorite source bonus
        bool hasFavoritedSource = candidate.MediaType == Config.MovieMediaType
            ? candidate.SourceIds.Any(id => profile.FavoritedMovieIds.Contains(id))
            : candidate.SourceIds.Any(id => profile.FavoritedTvIds.Contains(id));
        if (hasFavoritedSource)
            score += 1.0;

        return score;
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

        await Task.WhenAll(movieAffinityTask, tvAffinityTask);

        List<UserAffinitySourceDto> allSources = movieAffinityTask.Result
            .Concat(tvAffinityTask.Result).ToList();

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
