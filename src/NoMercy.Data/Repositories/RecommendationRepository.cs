using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class RecommendationCandidateDto
{
    public int MediaId { get; set; }
    public string? Title { get; set; }
    public string? TitleSort { get; set; }
    public string? Overview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string? SourceMediaType { get; set; }
    public int SourceCount { get; set; }
    public List<int> SourceIds { get; set; } = [];
}

public class UserAffinitySourceDto
{
    public int ItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public int? TimeWatched { get; set; }
    public int? Duration { get; set; }
    public bool IsFavorited { get; set; }
    public List<int> GenreIds { get; set; } = [];
    public List<int> KeywordIds { get; set; } = [];
}

public class RecommendationRepository
{
    public async Task<List<RecommendationCandidateDto>> GetUnownedMovieRecommendationsAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        // Step 1: Group server-side for IDs only (avoids SQL APPLY)
        // Use NOT EXISTS against Movies table instead of ToId==null (ToId may not be set for older data)
        List<int> mediaIds = await context.Recommendations
            .AsNoTracking()
            .Where(r => r.MovieFromId != null)
            .Where(r => r.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(r => !context.Movies.Any(m => m.Id == r.MediaId && m.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(r => r.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        // Step 2: Fetch metadata for each distinct MediaId
        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Recommendations
            .AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId) && r.MovieFromId != null)
            .Select(r => new { r.MediaId, r.Title, r.TitleSort, r.Overview, r.Poster, r.Backdrop, r._colorPalette, r.MovieFromId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .GroupBy(r => r.MediaId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RecommendationCandidateDto
                        {
                            MediaId = g.Key,
                            Title = first.Title,
                            TitleSort = first.TitleSort,
                            Overview = first.Overview,
                            Poster = first.Poster,
                            Backdrop = first.Backdrop,
                            ColorPalette = first._colorPalette,
                            MediaType = Config.MovieMediaType,
                            SourceCount = g.Select(r => r.MovieFromId).Distinct().Count(),
                            SourceIds = g.Select(r => r.MovieFromId!.Value).Distinct().ToList()
                        };
                    }), ct);

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedTvRecommendationsAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        List<int> mediaIds = await context.Recommendations
            .AsNoTracking()
            .Where(r => r.TvFromId != null)
            .Where(r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(r => r.TvFrom!.Library.Type != Config.AnimeMediaType)
            .Where(r => !context.Tvs.Any(t => t.Id == r.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(r => r.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Recommendations
            .AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId) && r.TvFromId != null)
            .Where(r => r.TvFrom!.Library.Type != Config.AnimeMediaType)
            .Select(r => new { r.MediaId, r.Title, r.TitleSort, r.Overview, r.Poster, r.Backdrop, r._colorPalette, r.TvFromId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .GroupBy(r => r.MediaId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RecommendationCandidateDto
                        {
                            MediaId = g.Key,
                            Title = first.Title,
                            TitleSort = first.TitleSort,
                            Overview = first.Overview,
                            Poster = first.Poster,
                            Backdrop = first.Backdrop,
                            ColorPalette = first._colorPalette,
                            MediaType = Config.TvMediaType,
                            SourceCount = g.Select(r => r.TvFromId).Distinct().Count(),
                            SourceIds = g.Select(r => r.TvFromId!.Value).Distinct().ToList()
                        };
                    }), ct);

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedAnimeRecommendationsAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        List<int> mediaIds = await context.Recommendations
            .AsNoTracking()
            .Where(r => r.TvFromId != null)
            .Where(r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(r => r.TvFrom!.Library.Type == Config.AnimeMediaType)
            .Where(r => !context.Tvs.Any(t => t.Id == r.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(r => r.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Recommendations
            .AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId) && r.TvFromId != null)
            .Where(r => r.TvFrom!.Library.Type == Config.AnimeMediaType)
            .Select(r => new { r.MediaId, r.Title, r.TitleSort, r.Overview, r.Poster, r.Backdrop, r._colorPalette, r.TvFromId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .GroupBy(r => r.MediaId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RecommendationCandidateDto
                        {
                            MediaId = g.Key,
                            Title = first.Title,
                            TitleSort = first.TitleSort,
                            Overview = first.Overview,
                            Poster = first.Poster,
                            Backdrop = first.Backdrop,
                            ColorPalette = first._colorPalette,
                            MediaType = Config.AnimeMediaType,
                            SourceCount = g.Select(r => r.TvFromId).Distinct().Count(),
                            SourceIds = g.Select(r => r.TvFromId!.Value).Distinct().ToList()
                        };
                    }), ct);

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedMovieSimilarAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        List<int> mediaIds = await context.Similar
            .AsNoTracking()
            .Where(s => s.MovieFromId != null)
            .Where(s => s.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(s => !context.Movies.Any(m => m.Id == s.MediaId && m.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Similar
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.MediaId) && s.MovieFromId != null)
            .Select(s => new { s.MediaId, s.Title, s.TitleSort, s.Overview, s.Poster, s.Backdrop, s._colorPalette, s.MovieFromId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .GroupBy(s => s.MediaId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RecommendationCandidateDto
                        {
                            MediaId = g.Key,
                            Title = first.Title,
                            TitleSort = first.TitleSort,
                            Overview = first.Overview,
                            Poster = first.Poster,
                            Backdrop = first.Backdrop,
                            ColorPalette = first._colorPalette,
                            MediaType = Config.MovieMediaType,
                            SourceCount = g.Select(s => s.MovieFromId).Distinct().Count(),
                            SourceIds = g.Select(s => s.MovieFromId!.Value).Distinct().ToList()
                        };
                    }), ct);

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedTvSimilarAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        List<int> mediaIds = await context.Similar
            .AsNoTracking()
            .Where(s => s.TvFromId != null)
            .Where(s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(s => s.TvFrom!.Library.Type != Config.AnimeMediaType)
            .Where(s => !context.Tvs.Any(t => t.Id == s.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Similar
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.MediaId) && s.TvFromId != null)
            .Where(s => s.TvFrom!.Library.Type != Config.AnimeMediaType)
            .Select(s => new { s.MediaId, s.Title, s.TitleSort, s.Overview, s.Poster, s.Backdrop, s._colorPalette, s.TvFromId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .GroupBy(s => s.MediaId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RecommendationCandidateDto
                        {
                            MediaId = g.Key,
                            Title = first.Title,
                            TitleSort = first.TitleSort,
                            Overview = first.Overview,
                            Poster = first.Poster,
                            Backdrop = first.Backdrop,
                            ColorPalette = first._colorPalette,
                            MediaType = Config.TvMediaType,
                            SourceCount = g.Select(s => s.TvFromId).Distinct().Count(),
                            SourceIds = g.Select(s => s.TvFromId!.Value).Distinct().ToList()
                        };
                    }), ct);

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedAnimeSimilarAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        List<int> mediaIds = await context.Similar
            .AsNoTracking()
            .Where(s => s.TvFromId != null)
            .Where(s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(s => s.TvFrom!.Library.Type == Config.AnimeMediaType)
            .Where(s => !context.Tvs.Any(t => t.Id == s.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Similar
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.MediaId) && s.TvFromId != null)
            .Where(s => s.TvFrom!.Library.Type == Config.AnimeMediaType)
            .Select(s => new { s.MediaId, s.Title, s.TitleSort, s.Overview, s.Poster, s.Backdrop, s._colorPalette, s.TvFromId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .GroupBy(s => s.MediaId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new RecommendationCandidateDto
                        {
                            MediaId = g.Key,
                            Title = first.Title,
                            TitleSort = first.TitleSort,
                            Overview = first.Overview,
                            Poster = first.Poster,
                            Backdrop = first.Backdrop,
                            ColorPalette = first._colorPalette,
                            MediaType = Config.AnimeMediaType,
                            SourceCount = g.Select(s => s.TvFromId).Distinct().Count(),
                            SourceIds = g.Select(s => s.TvFromId!.Value).Distinct().ToList()
                        };
                    }), ct);

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeTvCandidatesAsync(
        MediaContext context, Guid userId,
        Dictionary<int, List<int>> movieKeywordMap,
        int minSharedKeywords = 3, int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default)
    {
        if (movieKeywordMap.Count == 0) return [];

        HashSet<int> ownedMovieKeywordIds = movieKeywordMap.Values
            .SelectMany(kws => kws)
            .ToHashSet();

        if (ownedMovieKeywordIds.Count == 0) return [];

        // Filter out overly common keywords — generic tags like "animation" or "cat" match too many items
        HashSet<int> commonKeywordIds = (await context.KeywordTv
            .AsNoTracking()
            .Where(kt => ownedMovieKeywordIds.Contains(kt.KeywordId))
            .GroupBy(kt => kt.KeywordId)
            .Where(g => g.Count() > maxKeywordFrequency)
            .Select(g => g.Key)
            .ToListAsync(ct))
            .ToHashSet();

        HashSet<int> specificKeywordIds = ownedMovieKeywordIds
            .Where(id => !commonKeywordIds.Contains(id))
            .ToHashSet();

        if (specificKeywordIds.Count == 0) return [];

        // Step 1: Flat server-side query — find KeywordTv rows matching specific movie keywords on unowned TV shows (excluding anime)
        var keywordTvRows = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => specificKeywordIds.Contains(kt.KeywordId))
            .Where(kt => context.Tvs.Any(t => t.Id == kt.TvId && t.Library.Type != Config.AnimeMediaType))
            .Where(kt => !context.Tvs.Any(t => t.Id == kt.TvId && t.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(kt => new { kt.TvId, kt.KeywordId })
            .ToListAsync(ct);

        // Step 2: Client-side grouping — filter by minimum shared keyword count
        var tvKeywordGroups = keywordTvRows
            .GroupBy(r => r.TvId)
            .Where(g => g.Count() >= minSharedKeywords)
            .OrderByDescending(g => g.Count())
            .Take(maxCandidates)
            .ToList();

        if (tvKeywordGroups.Count == 0) return [];

        // Step 3: Fetch TV metadata for qualifying shows
        List<int> qualifyingTvIds = tvKeywordGroups.Select(g => g.Key).ToList();

        var tvMetadata = await context.Tvs
            .AsNoTracking()
            .Where(t => qualifyingTvIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title, t.TitleSort, t.Overview, t.Poster, t.Backdrop, t._colorPalette })
            .ToListAsync(ct);

        Dictionary<int, (string Title, string TitleSort, string? Overview, string? Poster, string? Backdrop, string Palette)> metaMap =
            tvMetadata.ToDictionary(t => t.Id, t => (t.Title, t.TitleSort, t.Overview, t.Poster, t.Backdrop, t._colorPalette));

        // Step 4: Build candidates with reverse-mapped source IDs
        return tvKeywordGroups
            .Where(g => metaMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var meta = metaMap[g.Key];
                HashSet<int> sharedKeywordIds = g.Select(r => r.KeywordId).ToHashSet();
                List<int> sourceMovieIds = movieKeywordMap
                    .Where(kv => kv.Value.Any(kw => sharedKeywordIds.Contains(kw)))
                    .Select(kv => kv.Key)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateDto
                {
                    MediaId = g.Key,
                    Title = meta.Title,
                    TitleSort = meta.TitleSort,
                    Overview = meta.Overview,
                    Poster = meta.Poster,
                    Backdrop = meta.Backdrop,
                    ColorPalette = meta.Palette,
                    MediaType = Config.TvMediaType,
                    SourceMediaType = Config.MovieMediaType,
                    SourceCount = sourceMovieIds.Count,
                    SourceIds = sourceMovieIds
                };
            })
            .ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeAnimeCandidatesAsync(
        MediaContext context, Guid userId,
        Dictionary<int, List<int>> movieKeywordMap,
        int minSharedKeywords = 3, int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default)
    {
        if (movieKeywordMap.Count == 0) return [];

        HashSet<int> ownedMovieKeywordIds = movieKeywordMap.Values
            .SelectMany(kws => kws)
            .ToHashSet();

        if (ownedMovieKeywordIds.Count == 0) return [];

        HashSet<int> commonKeywordIds = (await context.KeywordTv
            .AsNoTracking()
            .Where(kt => ownedMovieKeywordIds.Contains(kt.KeywordId))
            .GroupBy(kt => kt.KeywordId)
            .Where(g => g.Count() > maxKeywordFrequency)
            .Select(g => g.Key)
            .ToListAsync(ct))
            .ToHashSet();

        HashSet<int> specificKeywordIds = ownedMovieKeywordIds
            .Where(id => !commonKeywordIds.Contains(id))
            .ToHashSet();

        if (specificKeywordIds.Count == 0) return [];

        var keywordTvRows = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => specificKeywordIds.Contains(kt.KeywordId))
            .Where(kt => context.Tvs.Any(t => t.Id == kt.TvId && t.Library.Type == Config.AnimeMediaType))
            .Where(kt => !context.Tvs.Any(t => t.Id == kt.TvId && t.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(kt => new { kt.TvId, kt.KeywordId })
            .ToListAsync(ct);

        var tvKeywordGroups = keywordTvRows
            .GroupBy(r => r.TvId)
            .Where(g => g.Count() >= minSharedKeywords)
            .OrderByDescending(g => g.Count())
            .Take(maxCandidates)
            .ToList();

        if (tvKeywordGroups.Count == 0) return [];

        List<int> qualifyingTvIds = tvKeywordGroups.Select(g => g.Key).ToList();

        var tvMetadata = await context.Tvs
            .AsNoTracking()
            .Where(t => qualifyingTvIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title, t.TitleSort, t.Overview, t.Poster, t.Backdrop, t._colorPalette })
            .ToListAsync(ct);

        Dictionary<int, (string Title, string TitleSort, string? Overview, string? Poster, string? Backdrop, string Palette)> metaMap =
            tvMetadata.ToDictionary(t => t.Id, t => (t.Title, t.TitleSort, t.Overview, t.Poster, t.Backdrop, t._colorPalette));

        return tvKeywordGroups
            .Where(g => metaMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var meta = metaMap[g.Key];
                HashSet<int> sharedKeywordIds = g.Select(r => r.KeywordId).ToHashSet();
                List<int> sourceMovieIds = movieKeywordMap
                    .Where(kv => kv.Value.Any(kw => sharedKeywordIds.Contains(kw)))
                    .Select(kv => kv.Key)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateDto
                {
                    MediaId = g.Key,
                    Title = meta.Title,
                    TitleSort = meta.TitleSort,
                    Overview = meta.Overview,
                    Poster = meta.Poster,
                    Backdrop = meta.Backdrop,
                    ColorPalette = meta.Palette,
                    MediaType = Config.AnimeMediaType,
                    SourceMediaType = Config.MovieMediaType,
                    SourceCount = sourceMovieIds.Count,
                    SourceIds = sourceMovieIds
                };
            })
            .ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeMovieCandidatesAsync(
        MediaContext context, Guid userId,
        Dictionary<int, List<int>> tvKeywordMap,
        int minSharedKeywords = 3, int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default)
    {
        if (tvKeywordMap.Count == 0) return [];

        HashSet<int> ownedTvKeywordIds = tvKeywordMap.Values
            .SelectMany(kws => kws)
            .ToHashSet();

        if (ownedTvKeywordIds.Count == 0) return [];

        // Filter out overly common keywords — generic tags match too many items
        HashSet<int> commonKeywordIds = (await context.KeywordMovie
            .AsNoTracking()
            .Where(km => ownedTvKeywordIds.Contains(km.KeywordId))
            .GroupBy(km => km.KeywordId)
            .Where(g => g.Count() > maxKeywordFrequency)
            .Select(g => g.Key)
            .ToListAsync(ct))
            .ToHashSet();

        HashSet<int> specificKeywordIds = ownedTvKeywordIds
            .Where(id => !commonKeywordIds.Contains(id))
            .ToHashSet();

        if (specificKeywordIds.Count == 0) return [];

        // Step 1: Flat server-side query — find KeywordMovie rows matching specific TV keywords on unowned movies
        var keywordMovieRows = await context.KeywordMovie
            .AsNoTracking()
            .Where(km => specificKeywordIds.Contains(km.KeywordId))
            .Where(km => !context.Movies.Any(m => m.Id == km.MovieId && m.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Select(km => new { km.MovieId, km.KeywordId })
            .ToListAsync(ct);

        // Step 2: Client-side grouping — filter by minimum shared keyword count
        var movieKeywordGroups = keywordMovieRows
            .GroupBy(r => r.MovieId)
            .Where(g => g.Count() >= minSharedKeywords)
            .OrderByDescending(g => g.Count())
            .Take(maxCandidates)
            .ToList();

        if (movieKeywordGroups.Count == 0) return [];

        // Step 3: Fetch movie metadata for qualifying movies
        List<int> qualifyingMovieIds = movieKeywordGroups.Select(g => g.Key).ToList();

        var movieMetadata = await context.Movies
            .AsNoTracking()
            .Where(m => qualifyingMovieIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Title, m.TitleSort, m.Overview, m.Poster, m.Backdrop, m._colorPalette })
            .ToListAsync(ct);

        Dictionary<int, (string Title, string TitleSort, string? Overview, string? Poster, string? Backdrop, string Palette)> metaMap =
            movieMetadata.ToDictionary(m => m.Id, m => (m.Title, m.TitleSort, m.Overview, m.Poster, m.Backdrop, m._colorPalette));

        // Step 4: Build candidates with reverse-mapped source IDs
        return movieKeywordGroups
            .Where(g => metaMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var meta = metaMap[g.Key];
                HashSet<int> sharedKeywordIds = g.Select(r => r.KeywordId).ToHashSet();
                List<int> sourceTvIds = tvKeywordMap
                    .Where(kv => kv.Value.Any(kw => sharedKeywordIds.Contains(kw)))
                    .Select(kv => kv.Key)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateDto
                {
                    MediaId = g.Key,
                    Title = meta.Title,
                    TitleSort = meta.TitleSort,
                    Overview = meta.Overview,
                    Poster = meta.Poster,
                    Backdrop = meta.Backdrop,
                    ColorPalette = meta.Palette,
                    MediaType = Config.MovieMediaType,
                    SourceMediaType = Config.TvMediaType,
                    SourceCount = sourceTvIds.Count,
                    SourceIds = sourceTvIds
                };
            })
            .ToList();
    }

    public async Task<List<UserAffinitySourceDto>> GetUserMovieAffinityDataAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        // Fetch flat data without nested collection projections to avoid SQL APPLY
        var movies = await context.Movies
            .AsNoTracking()
            .Where(m => m.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(m => m.VideoFiles.Any())
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Poster,
                m._colorPalette,
                m.Runtime,
                Rating = m.UserData
                    .Where(ud => ud.UserId == userId && ud.Rating != null)
                    .Select(ud => ud.Rating)
                    .FirstOrDefault(),
                TimeWatched = m.UserData
                    .Where(ud => ud.UserId == userId)
                    .OrderByDescending(ud => ud.Time)
                    .Select(ud => ud.Time)
                    .FirstOrDefault(),
                IsFavorited = m.MovieUser.Any(mu => mu.UserId == userId)
            })
            .ToListAsync(ct);

        if (movies.Count == 0) return [];

        List<int> movieIds = movies.Select(m => m.Id).ToList();

        // Fetch genre and keyword mappings separately
        Dictionary<int, List<int>> genreMap = await context.GenreMovie
            .AsNoTracking()
            .Where(gm => movieIds.Contains(gm.MovieId))
            .GroupBy(gm => gm.MovieId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(gm => gm.GenreId).ToList(), ct);

        Dictionary<int, List<int>> keywordMap = await context.KeywordMovie
            .AsNoTracking()
            .Where(km => movieIds.Contains(km.MovieId))
            .GroupBy(km => km.MovieId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(km => km.KeywordId).ToList(), ct);

        return movies.Select(m => new UserAffinitySourceDto
        {
            ItemId = m.Id,
            Title = m.Title,
            Poster = m.Poster,
            ColorPalette = m._colorPalette,
            MediaType = Config.MovieMediaType,
            Rating = m.Rating,
            TimeWatched = m.TimeWatched,
            Duration = m.Runtime != null ? m.Runtime * 60 : null,
            IsFavorited = m.IsFavorited,
            GenreIds = genreMap.GetValueOrDefault(m.Id, []),
            KeywordIds = keywordMap.GetValueOrDefault(m.Id, [])
        }).ToList();
    }

    public async Task<List<UserAffinitySourceDto>> GetUserTvAffinityDataAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        var tvShows = await context.Tvs
            .AsNoTracking()
            .Where(t => t.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(t => t.Library.Type != Config.AnimeMediaType)
            .Where(t => t.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Poster,
                t._colorPalette,
                t.Duration,
                Rating = t.UserData
                    .Where(ud => ud.UserId == userId && ud.Rating != null)
                    .Select(ud => ud.Rating)
                    .FirstOrDefault(),
                TimeWatched = t.UserData
                    .Where(ud => ud.UserId == userId)
                    .OrderByDescending(ud => ud.Time)
                    .Select(ud => ud.Time)
                    .FirstOrDefault(),
                IsFavorited = t.TvUser.Any(tu => tu.UserId == userId)
            })
            .ToListAsync(ct);

        if (tvShows.Count == 0) return [];

        List<int> tvIds = tvShows.Select(t => t.Id).ToList();

        Dictionary<int, List<int>> genreMap = await context.GenreTv
            .AsNoTracking()
            .Where(gt => tvIds.Contains(gt.TvId))
            .GroupBy(gt => gt.TvId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(gt => gt.GenreId).ToList(), ct);

        Dictionary<int, List<int>> keywordMap = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => tvIds.Contains(kt.TvId))
            .GroupBy(kt => kt.TvId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(kt => kt.KeywordId).ToList(), ct);

        return tvShows.Select(t => new UserAffinitySourceDto
        {
            ItemId = t.Id,
            Title = t.Title,
            Poster = t.Poster,
            ColorPalette = t._colorPalette,
            MediaType = Config.TvMediaType,
            Rating = t.Rating,
            TimeWatched = t.TimeWatched,
            Duration = t.Duration,
            IsFavorited = t.IsFavorited,
            GenreIds = genreMap.GetValueOrDefault(t.Id, []),
            KeywordIds = keywordMap.GetValueOrDefault(t.Id, [])
        }).ToList();
    }

    public async Task<List<UserAffinitySourceDto>> GetUserAnimeAffinityDataAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        var animeShows = await context.Tvs
            .AsNoTracking()
            .Where(t => t.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(t => t.Library.Type == Config.AnimeMediaType)
            .Where(t => t.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Poster,
                t._colorPalette,
                t.Duration,
                Rating = t.UserData
                    .Where(ud => ud.UserId == userId && ud.Rating != null)
                    .Select(ud => ud.Rating)
                    .FirstOrDefault(),
                TimeWatched = t.UserData
                    .Where(ud => ud.UserId == userId)
                    .OrderByDescending(ud => ud.Time)
                    .Select(ud => ud.Time)
                    .FirstOrDefault(),
                IsFavorited = t.TvUser.Any(tu => tu.UserId == userId)
            })
            .ToListAsync(ct);

        if (animeShows.Count == 0) return [];

        List<int> animeIds = animeShows.Select(t => t.Id).ToList();

        Dictionary<int, List<int>> genreMap = await context.GenreTv
            .AsNoTracking()
            .Where(gt => animeIds.Contains(gt.TvId))
            .GroupBy(gt => gt.TvId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(gt => gt.GenreId).ToList(), ct);

        Dictionary<int, List<int>> keywordMap = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => animeIds.Contains(kt.TvId))
            .GroupBy(kt => kt.TvId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(kt => kt.KeywordId).ToList(), ct);

        return animeShows.Select(t => new UserAffinitySourceDto
        {
            ItemId = t.Id,
            Title = t.Title,
            Poster = t.Poster,
            ColorPalette = t._colorPalette,
            MediaType = Config.AnimeMediaType,
            Rating = t.Rating,
            TimeWatched = t.TimeWatched,
            Duration = t.Duration,
            IsFavorited = t.IsFavorited,
            GenreIds = genreMap.GetValueOrDefault(t.Id, []),
            KeywordIds = keywordMap.GetValueOrDefault(t.Id, [])
        }).ToList();
    }

    public async Task<Dictionary<int, List<int>>> GetGenresForMovieIdsAsync(
        MediaContext context, List<int> movieIds, CancellationToken ct = default)
    {
        if (movieIds.Count == 0) return new();

        return await context.GenreMovie
            .AsNoTracking()
            .Where(gm => movieIds.Contains(gm.MovieId))
            .GroupBy(gm => gm.MovieId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(gm => gm.GenreId).ToList(),
                ct);
    }

    public async Task<Dictionary<int, List<int>>> GetGenresForTvIdsAsync(
        MediaContext context, List<int> tvIds, CancellationToken ct = default)
    {
        if (tvIds.Count == 0) return new();

        return await context.GenreTv
            .AsNoTracking()
            .Where(gt => tvIds.Contains(gt.TvId))
            .GroupBy(gt => gt.TvId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(gt => gt.GenreId).ToList(),
                ct);
    }

    public async Task<(List<Movie> Movies, string? ColorPalette)> GetSourceMoviesForMediaAsync(
        MediaContext context, Guid userId, int mediaId, CancellationToken ct = default)
    {
        // Get distinct source movie IDs and grab color palette from the same query
        var recRows = await context.Recommendations
            .AsNoTracking()
            .Where(r => r.MediaId == mediaId && r.MovieFromId != null)
            .Where(r => r.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(r => new { SourceId = r.MovieFromId!.Value, r._colorPalette })
            .ToListAsync(ct);

        var simRows = await context.Similar
            .AsNoTracking()
            .Where(s => s.MediaId == mediaId && s.MovieFromId != null)
            .Where(s => s.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(s => new { SourceId = s.MovieFromId!.Value, s._colorPalette })
            .ToListAsync(ct);

        var allRows = recRows.Concat(simRows).ToList();
        string? colorPalette = allRows.FirstOrDefault(r => !string.IsNullOrEmpty(r._colorPalette))?._colorPalette;
        List<int> sourceIds = allRows.Select(r => r.SourceId).Distinct().ToList();

        if (sourceIds.Count == 0) return ([], colorPalette);

        List<Movie> movies = await context.Movies
            .AsNoTracking()
            .Where(m => sourceIds.Contains(m.Id))
            .Where(m => m.VideoFiles.Any())
            .Include(m => m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage))
            .Include(m => m.VideoFiles)
            .Include(m => m.KeywordMovies).ThenInclude(km => km.Keyword)
            .ToListAsync(ct);

        return (movies, colorPalette);
    }

    public async Task<(List<Tv> TvShows, string? ColorPalette)> GetSourceTvShowsForMediaAsync(
        MediaContext context, Guid userId, int mediaId, CancellationToken ct = default)
    {
        var recRows = await context.Recommendations
            .AsNoTracking()
            .Where(r => r.MediaId == mediaId && r.TvFromId != null)
            .Where(r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(r => new { SourceId = r.TvFromId!.Value, r._colorPalette })
            .ToListAsync(ct);

        var simRows = await context.Similar
            .AsNoTracking()
            .Where(s => s.MediaId == mediaId && s.TvFromId != null)
            .Where(s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(s => new { SourceId = s.TvFromId!.Value, s._colorPalette })
            .ToListAsync(ct);

        var allRows = recRows.Concat(simRows).ToList();
        string? colorPalette = allRows.FirstOrDefault(r => !string.IsNullOrEmpty(r._colorPalette))?._colorPalette;
        List<int> sourceIds = allRows.Select(r => r.SourceId).Distinct().ToList();

        if (sourceIds.Count == 0) return ([], colorPalette);

        List<Tv> tvShows = await context.Tvs
            .AsNoTracking()
            .Where(t => sourceIds.Contains(t.Id))
            .Where(t => t.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Include(t => t.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage))
            .Include(t => t.Episodes).ThenInclude(e => e.VideoFiles)
            .Include(t => t.KeywordTvs).ThenInclude(kt => kt.Keyword)
            .ToListAsync(ct);

        return (tvShows, colorPalette);
    }

    public async Task<List<Movie>> GetKeywordMovieSourcesForMovieAsync(
        MediaContext context, Guid userId, int movieId, HashSet<int> excludeIds, CancellationToken ct = default)
    {
        List<int> targetKeywordIds = await context.KeywordMovie
            .AsNoTracking()
            .Where(km => km.MovieId == movieId)
            .Select(km => km.KeywordId)
            .ToListAsync(ct);

        if (targetKeywordIds.Count == 0) return [];

        List<int> matchingMovieIds = await context.KeywordMovie
            .AsNoTracking()
            .Where(km => targetKeywordIds.Contains(km.KeywordId))
            .Where(km => km.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(km => km.Movie.VideoFiles.Any())
            .Where(km => !excludeIds.Contains(km.MovieId))
            .Select(km => km.MovieId)
            .Distinct()
            .ToListAsync(ct);

        if (matchingMovieIds.Count == 0) return [];

        return await context.Movies
            .AsNoTracking()
            .Where(m => matchingMovieIds.Contains(m.Id))
            .Include(m => m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage))
            .Include(m => m.VideoFiles)
            .Include(m => m.KeywordMovies).ThenInclude(km => km.Keyword)
            .ToListAsync(ct);
    }

    public async Task<List<Tv>> GetKeywordTvSourcesForTvAsync(
        MediaContext context, Guid userId, int tvId, HashSet<int> excludeIds, CancellationToken ct = default)
    {
        List<int> targetKeywordIds = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => kt.TvId == tvId)
            .Select(kt => kt.KeywordId)
            .ToListAsync(ct);

        if (targetKeywordIds.Count == 0) return [];

        List<int> matchingTvIds = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => targetKeywordIds.Contains(kt.KeywordId))
            .Where(kt => kt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(kt => kt.Tv.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Where(kt => !excludeIds.Contains(kt.TvId))
            .Select(kt => kt.TvId)
            .Distinct()
            .ToListAsync(ct);

        if (matchingTvIds.Count == 0) return [];

        return await context.Tvs
            .AsNoTracking()
            .Where(t => matchingTvIds.Contains(t.Id))
            .Include(t => t.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage))
            .Include(t => t.Episodes).ThenInclude(e => e.VideoFiles)
            .Include(t => t.KeywordTvs).ThenInclude(kt => kt.Keyword)
            .ToListAsync(ct);
    }

    public async Task<List<Movie>> GetCrossTypeMovieSourcesForTvAsync(
        MediaContext context, Guid userId, int tvId, CancellationToken ct = default)
    {
        List<int> tvKeywordIds = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => kt.TvId == tvId)
            .Select(kt => kt.KeywordId)
            .ToListAsync(ct);

        if (tvKeywordIds.Count == 0) return [];

        List<int> movieIds = await context.KeywordMovie
            .AsNoTracking()
            .Where(km => tvKeywordIds.Contains(km.KeywordId))
            .Where(km => km.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(km => km.Movie.VideoFiles.Any())
            .Select(km => km.MovieId)
            .Distinct()
            .ToListAsync(ct);

        if (movieIds.Count == 0) return [];

        return await context.Movies
            .AsNoTracking()
            .Where(m => movieIds.Contains(m.Id))
            .Include(m => m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage))
            .Include(m => m.VideoFiles)
            .Include(m => m.KeywordMovies).ThenInclude(km => km.Keyword)
            .ToListAsync(ct);
    }

    public async Task<List<Tv>> GetCrossTypeTvSourcesForMovieAsync(
        MediaContext context, Guid userId, int movieId, CancellationToken ct = default)
    {
        List<int> movieKeywordIds = await context.KeywordMovie
            .AsNoTracking()
            .Where(km => km.MovieId == movieId)
            .Select(km => km.KeywordId)
            .ToListAsync(ct);

        if (movieKeywordIds.Count == 0) return [];

        List<int> tvIds = await context.KeywordTv
            .AsNoTracking()
            .Where(kt => movieKeywordIds.Contains(kt.KeywordId))
            .Where(kt => kt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(kt => kt.Tv.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Select(kt => kt.TvId)
            .Distinct()
            .ToListAsync(ct);

        if (tvIds.Count == 0) return [];

        return await context.Tvs
            .AsNoTracking()
            .Where(t => tvIds.Contains(t.Id))
            .Include(t => t.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage))
            .Include(t => t.Episodes).ThenInclude(e => e.VideoFiles)
            .Include(t => t.KeywordTvs).ThenInclude(kt => kt.Keyword)
            .ToListAsync(ct);
    }
}
