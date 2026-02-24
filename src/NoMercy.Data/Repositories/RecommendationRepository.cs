using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
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
    public int SourceCount { get; set; }
    public List<int> SourceIds { get; set; } = [];
}

public class UserAffinitySourceDto
{
    public int ItemId { get; set; }
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
        List<int> mediaIds = await context.Recommendations
            .AsNoTracking()
            .Where(r => r.MovieFromId != null && r.MovieToId == null)
            .Where(r => r.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(r => r.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        // Step 2: Fetch metadata for each distinct MediaId
        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Recommendations
            .AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId) && r.MovieFromId != null && r.MovieToId == null)
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
            .Where(r => r.TvFromId != null && r.TvToId == null)
            .Where(r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(r => r.TvFrom!.Library.Type != Config.AnimeMediaType)
            .Select(r => r.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Recommendations
            .AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId) && r.TvFromId != null && r.TvToId == null)
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
            .Where(r => r.TvFromId != null && r.TvToId == null)
            .Where(r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(r => r.TvFrom!.Library.Type == Config.AnimeMediaType)
            .Select(r => r.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Recommendations
            .AsNoTracking()
            .Where(r => mediaIds.Contains(r.MediaId) && r.TvFromId != null && r.TvToId == null)
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
            .Where(s => s.MovieFromId != null && s.MovieToId == null)
            .Where(s => s.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Similar
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.MediaId) && s.MovieFromId != null && s.MovieToId == null)
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
            .Where(s => s.TvFromId != null && s.TvToId == null)
            .Where(s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(s => s.TvFrom!.Library.Type != Config.AnimeMediaType)
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Similar
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.MediaId) && s.TvFromId != null && s.TvToId == null)
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
            .Where(s => s.TvFromId != null && s.TvToId == null)
            .Where(s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(s => s.TvFrom!.Library.Type == Config.AnimeMediaType)
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(ct);

        if (mediaIds.Count == 0) return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context.Similar
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.MediaId) && s.TvFromId != null && s.TvToId == null)
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

    public async Task<List<UserAffinitySourceDto>> GetUserMovieAffinityDataAsync(
        MediaContext context, Guid userId, CancellationToken ct = default)
    {
        // Fetch flat data without nested collection projections to avoid SQL APPLY
        var movies = await context.Movies
            .AsNoTracking()
            .Where(m => m.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(m => new
            {
                m.Id,
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
            .Select(t => new
            {
                t.Id,
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
            MediaType = Config.TvMediaType,
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
}
