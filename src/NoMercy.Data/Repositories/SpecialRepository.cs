using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class SpecialCardDto
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public string? Logo { get; set; }
    public string? ColorPalette { get; set; }
    public DateTime CreatedAt { get; set; }
    public int NumberOfItems { get; set; }
    public int HaveMovies { get; set; }
    public int HaveEpisodes { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
}

public class SpecialDetailDto
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Backdrop { get; set; }
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public int NumberOfItems { get; set; }
    public int HaveMovies { get; set; }
    public int HaveEpisodes { get; set; }
    public List<SpecialItemRefDto> Items { get; set; } = [];
}

public class SpecialItemRefDto
{
    public int? MovieId { get; set; }
    public int? EpisodeId { get; set; }
    public int TvId { get; set; }
}

public class SpecialMovieProjection
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Backdrop { get; set; }
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public int? Runtime { get; set; }
    public double? VoteAverage { get; set; }
    public string? Video { get; set; }
    public string? Logo { get; set; }
    public int VideoFileCount { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
    public List<SpecialGenreProjection> Genres { get; set; } = [];
    public List<SpecialImageProjection> Backdrops { get; set; } = [];
    public List<SpecialImageProjection> Posters { get; set; } = [];
    public List<SpecialCastProjection> Cast { get; set; } = [];
    public List<SpecialCrewProjection> Crew { get; set; } = [];
}

public class SpecialTvProjection
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Backdrop { get; set; }
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public DateTime? FirstAirDate { get; set; }
    public int? Duration { get; set; }
    public double? VoteAverage { get; set; }
    public string? Trailer { get; set; }
    public string? Logo { get; set; }
    public int NumberOfEpisodes { get; set; }
    public int HaveEpisodes { get; set; }
    public int[] EpisodeIds { get; set; } = [];
    public List<string?> EpisodeDurations { get; set; } = [];
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
    public List<SpecialGenreProjection> Genres { get; set; } = [];
    public List<SpecialImageProjection> Backdrops { get; set; } = [];
    public List<SpecialImageProjection> Posters { get; set; } = [];
    public List<SpecialCastProjection> Cast { get; set; } = [];
    public List<SpecialCrewProjection> Crew { get; set; } = [];
}

public class SpecialGenreProjection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SpecialImageProjection
{
    public int Id { get; set; }
    public string? Site { get; set; }
    public string? FilePath { get; set; }
    public int Width { get; set; }
    public string? Type { get; set; }
    public int Height { get; set; }
    public string? Iso6391 { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public string? ColorPalette { get; set; }
}

public class SpecialCastProjection
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonProfile { get; set; }
    public string? PersonKnownForDepartment { get; set; }
    public string? PersonColorPalette { get; set; }
    public DateTime? PersonDeathDay { get; set; }
    public string PersonGender { get; set; } = string.Empty;
    public string? Character { get; set; }
    public int? Order { get; set; }
}

public class SpecialCrewProjection
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonProfile { get; set; }
    public string? PersonKnownForDepartment { get; set; }
    public string? PersonColorPalette { get; set; }
    public DateTime? PersonDeathDay { get; set; }
    public string PersonGender { get; set; } = string.Empty;
    public string? Task { get; set; }
    public int? Order { get; set; }
}

public class SpecialRepository(MediaContext context)
{
    public async Task<List<Special>> GetSpecialsAsync(Guid userId, string language, int take, int page, CancellationToken ct = default)
    {
        List<Special> specials = await context.Specials
            .AsNoTracking()
            .AsSplitQuery()
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.VideoFiles.Where(v => v.Folder != null))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.VideoFiles.Where(v => v.Folder != null))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.CertificationMovies.Where(c => c.Certification.Iso31661 == "US").Take(1))
                .ThenInclude(c => c.Certification)
            .OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);

        return specials;
    }

    public Task<List<SpecialCardDto>> GetSpecialCardsAsync(Guid userId, string language, int take, int page, CancellationToken ct = default)
    {
        return context.Specials
            .AsNoTracking()
            .OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take)
            .Select(special => new SpecialCardDto
            {
                Id = special.Id,
                Title = special.Title,
                TitleSort = special.TitleSort,
                Overview = special.Overview,
                Poster = special.Poster,
                Backdrop = special.Backdrop,
                Logo = special.Logo,
                ColorPalette = special._colorPalette,
                CreatedAt = special.CreatedAt,
                NumberOfItems = special.Items.Count,
                HaveMovies = special.Items.Count(i => i.Movie != null && i.Movie.VideoFiles.Any(v => v.Folder != null)),
                HaveEpisodes = special.Items.Count(i => i.Episode != null && i.Episode.VideoFiles.Any(v => v.Folder != null)),
                CertificationRating = special.Items
                    .Where(i => i.Movie != null)
                    .SelectMany(i => i.Movie!.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US")
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = special.Items
                    .Where(i => i.Movie != null)
                    .SelectMany(i => i.Movie!.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US")
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public Task<List<SpecialCardDto>> GetSpecialItemCardsAsync(Guid userId, string language, string country, int take = 1, int page = 0, CancellationToken ct = default)
    {
        return context.Specials
            .AsNoTracking()
            .OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take)
            .Select(special => new SpecialCardDto
            {
                Id = special.Id,
                Title = special.Title,
                TitleSort = special.TitleSort,
                Overview = special.Overview,
                Poster = special.Poster,
                Backdrop = special.Backdrop,
                Logo = special.Logo,
                ColorPalette = special._colorPalette,
                CreatedAt = special.CreatedAt,
                NumberOfItems = special.Items.Count,
                HaveMovies = special.Items.Count(i => i.Movie != null && i.Movie.VideoFiles.Any(v => v.Folder != null)),
                HaveEpisodes = special.Items.Count(i => i.Episode != null && i.Episode.VideoFiles.Any(v => v.Folder != null)),
                CertificationRating = special.Items
                    .Where(i => i.Movie != null)
                    .SelectMany(i => i.Movie!.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country)
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = special.Items
                    .Where(i => i.Movie != null)
                    .SelectMany(i => i.Movie!.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country)
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public Task<SpecialDetailDto?> GetSpecialDetailAsync(Guid userId, Ulid id, CancellationToken ct = default)
    {
        return context.Specials
            .AsNoTracking()
            .AsSplitQuery()
            .Where(special => special.Id == id)
            .Select(special => new SpecialDetailDto
            {
                Id = special.Id,
                Title = special.Title,
                Overview = special.Overview,
                Backdrop = special.Backdrop,
                Poster = special.Poster,
                ColorPalette = special._colorPalette,
                Favorite = special.SpecialUser.Any(su => su.UserId == userId),
                NumberOfItems = special.Items.Count,
                HaveMovies = special.Items.Count(i => i.Movie != null && i.Movie.VideoFiles.Any(v => v.Folder != null)),
                HaveEpisodes = special.Items.Count(i => i.Episode != null && i.Episode.VideoFiles.Any(v => v.Folder != null)),
                Items = special.Items
                    .OrderBy(i => i.Order)
                    .Select(i => new SpecialItemRefDto
                    {
                        MovieId = i.MovieId,
                        EpisodeId = i.EpisodeId,
                        TvId = i.Episode != null ? i.Episode.TvId : 0
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public static Task<List<SpecialMovieProjection>> GetSpecialMovieProjectionsAsync(
        MediaContext ctx, Guid userId, IEnumerable<int> movieIds, string country, CancellationToken ct = default)
    {
        return ctx.Movies
            .AsNoTracking()
            .AsSplitQuery()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new SpecialMovieProjection
            {
                Id = movie.Id,
                Title = movie.Title,
                Overview = movie.Overview,
                Backdrop = movie.Backdrop,
                Poster = movie.Poster,
                ColorPalette = movie._colorPalette,
                ReleaseDate = movie.ReleaseDate,
                Runtime = movie.Runtime,
                VoteAverage = movie.VoteAverage,
                Video = movie.Video,
                Logo = movie.Images
                    .Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                VideoFileCount = movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = movie.CertificationMovies
                    .Where(cm => cm.Certification.Iso31661 == country || cm.Certification.Iso31661 == "US")
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = movie.CertificationMovies
                    .Where(cm => cm.Certification.Iso31661 == country || cm.Certification.Iso31661 == "US")
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault(),
                Genres = movie.GenreMovies.Select(gm => new SpecialGenreProjection
                {
                    Id = gm.GenreId,
                    Name = gm.Genre.Name
                }).ToList(),
                Backdrops = movie.Images
                    .Where(i => i.Type == "backdrop" && (i.Iso6391 == "en" || i.Iso6391 == null))
                    .OrderByDescending(i => i.VoteAverage)
                    .Take(2)
                    .Select(i => new SpecialImageProjection
                    {
                        Id = i.Id,
                        Site = i.Site,
                        FilePath = i.FilePath,
                        Width = i.Width ?? 0,
                        Type = i.Type,
                        Height = i.Height ?? 0,
                        Iso6391 = i.Iso6391,
                        VoteAverage = i.VoteAverage ?? 0,
                        VoteCount = i.VoteCount ?? 0,
                        ColorPalette = i._colorPalette
                    }).ToList(),
                Posters = movie.Images
                    .Where(i => i.Type == "poster" && (i.Iso6391 == "en" || i.Iso6391 == null))
                    .OrderByDescending(i => i.VoteAverage)
                    .Take(2)
                    .Select(i => new SpecialImageProjection
                    {
                        Id = i.Id,
                        Site = i.Site,
                        FilePath = i.FilePath,
                        Width = i.Width ?? 0,
                        Type = i.Type,
                        Height = i.Height ?? 0,
                        Iso6391 = i.Iso6391,
                        VoteAverage = i.VoteAverage ?? 0,
                        VoteCount = i.VoteCount ?? 0,
                        ColorPalette = i._colorPalette
                    }).ToList(),
                Cast = movie.Cast
                    .OrderBy(c => c.Role.Order)
                    .Take(15)
                    .Select(c => new SpecialCastProjection
                    {
                        PersonId = c.Person.Id,
                        PersonName = c.Person.Name,
                        PersonProfile = c.Person.Profile,
                        PersonKnownForDepartment = c.Person.KnownForDepartment,
                        PersonColorPalette = c.Person._colorPalette,
                        PersonDeathDay = c.Person.DeathDay,
                        PersonGender = c.Person.Gender,
                        Character = c.Role.Character,
                        Order = c.Role.Order
                    }).ToList(),
                Crew = movie.Crew
                    .Take(15)
                    .Select(c => new SpecialCrewProjection
                    {
                        PersonId = c.Person.Id,
                        PersonName = c.Person.Name,
                        PersonProfile = c.Person.Profile,
                        PersonKnownForDepartment = c.Person.KnownForDepartment,
                        PersonColorPalette = c.Person._colorPalette,
                        PersonDeathDay = c.Person.DeathDay,
                        PersonGender = c.Person.Gender,
                        Task = c.Job.Task,
                        Order = c.Job.Order
                    }).ToList()
            })
            .ToListAsync(ct);
    }

    public static Task<List<SpecialTvProjection>> GetSpecialTvProjectionsAsync(
        MediaContext ctx, Guid userId, IEnumerable<int> tvIds, string country, CancellationToken ct = default)
    {
        return ctx.Tvs
            .AsNoTracking()
            .AsSplitQuery()
            .Where(tv => tvIds.Contains(tv.Id))
            .Select(tv => new SpecialTvProjection
            {
                Id = tv.Id,
                Title = tv.Title,
                Overview = tv.Overview,
                Backdrop = tv.Backdrop,
                Poster = tv.Poster,
                ColorPalette = tv._colorPalette,
                FirstAirDate = tv.FirstAirDate,
                Duration = tv.Duration,
                VoteAverage = tv.VoteAverage,
                Trailer = tv.Trailer,
                Logo = tv.Images
                    .Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                NumberOfEpisodes = tv.Episodes.Count(e => e.SeasonNumber > 0),
                HaveEpisodes = tv.Episodes.Count(e => e.SeasonNumber > 0 && e.VideoFiles.Any()),
                EpisodeIds = tv.Episodes.Select(e => e.Id).ToArray(),
                EpisodeDurations = tv.Episodes
                    .Where(e => e.SeasonNumber > 0)
                    .Select(e => e.VideoFiles.Select(vf => vf.Duration).FirstOrDefault())
                    .ToList(),
                CertificationRating = tv.CertificationTvs
                    .Where(ct2 => ct2.Certification.Iso31661 == country || ct2.Certification.Iso31661 == "US")
                    .Select(ct2 => ct2.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = tv.CertificationTvs
                    .Where(ct2 => ct2.Certification.Iso31661 == country || ct2.Certification.Iso31661 == "US")
                    .Select(ct2 => ct2.Certification.Iso31661)
                    .FirstOrDefault(),
                Genres = tv.GenreTvs.Select(gt => new SpecialGenreProjection
                {
                    Id = gt.GenreId,
                    Name = gt.Genre.Name
                }).ToList(),
                Backdrops = tv.Images
                    .Where(i => i.Type == "backdrop" && (i.Iso6391 == "en" || i.Iso6391 == null))
                    .OrderByDescending(i => i.VoteAverage)
                    .Take(2)
                    .Select(i => new SpecialImageProjection
                    {
                        Id = i.Id,
                        Site = i.Site,
                        FilePath = i.FilePath,
                        Width = i.Width ?? 0,
                        Type = i.Type,
                        Height = i.Height ?? 0,
                        Iso6391 = i.Iso6391,
                        VoteAverage = i.VoteAverage ?? 0,
                        VoteCount = i.VoteCount ?? 0,
                        ColorPalette = i._colorPalette
                    }).ToList(),
                Posters = tv.Images
                    .Where(i => i.Type == "poster" && (i.Iso6391 == "en" || i.Iso6391 == null))
                    .OrderByDescending(i => i.VoteAverage)
                    .Take(2)
                    .Select(i => new SpecialImageProjection
                    {
                        Id = i.Id,
                        Site = i.Site,
                        FilePath = i.FilePath,
                        Width = i.Width ?? 0,
                        Type = i.Type,
                        Height = i.Height ?? 0,
                        Iso6391 = i.Iso6391,
                        VoteAverage = i.VoteAverage ?? 0,
                        VoteCount = i.VoteCount ?? 0,
                        ColorPalette = i._colorPalette
                    }).ToList(),
                Cast = tv.Cast
                    .OrderBy(c => c.Role.Order)
                    .Take(15)
                    .Select(c => new SpecialCastProjection
                    {
                        PersonId = c.Person.Id,
                        PersonName = c.Person.Name,
                        PersonProfile = c.Person.Profile,
                        PersonKnownForDepartment = c.Person.KnownForDepartment,
                        PersonColorPalette = c.Person._colorPalette,
                        PersonDeathDay = c.Person.DeathDay,
                        PersonGender = c.Person.Gender,
                        Character = c.Role.Character,
                        Order = c.Role.Order
                    }).ToList(),
                Crew = tv.Crew
                    .Take(15)
                    .Select(c => new SpecialCrewProjection
                    {
                        PersonId = c.Person.Id,
                        PersonName = c.Person.Name,
                        PersonProfile = c.Person.Profile,
                        PersonKnownForDepartment = c.Person.KnownForDepartment,
                        PersonColorPalette = c.Person._colorPalette,
                        PersonDeathDay = c.Person.DeathDay,
                        PersonGender = c.Person.Gender,
                        Task = c.Job.Task,
                        Order = c.Job.Order
                    }).ToList()
            })
            .ToListAsync(ct);
    }

    public Task<Special?> GetSpecialAsync(Guid userId, Ulid id, CancellationToken ct = default)
    {
        return Task.FromResult(context.Specials
            .AsNoTracking()
            .AsSplitQuery()
            .Where(special => special.Id == id)
            .Include(special => special.Items
                .OrderBy(specialItem => specialItem.Order)
            )
            .ThenInclude(specialItem => specialItem.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(userData => userData.UserId.Equals(userId))
            )
            .Include(special => special.Items
                .OrderBy(specialItem => specialItem.Order)
            )
            .ThenInclude(specialItem => specialItem.Episode)
            .ThenInclude(movie => movie!.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(userData => userData.UserId.Equals(userId))
            )
            .Include(special => special.SpecialUser
                .Where(specialUser => specialUser.UserId.Equals(userId))
            )
            .FirstOrDefault());
    }

    public Task<List<Special>> GetSpecialItems(Guid userId, string? language, string country, int take = 1, int page = 0, CancellationToken ct = default)
    {
        return context.Specials
            .AsNoTracking()
            .AsSplitQuery()
            .Include(special => special.SpecialUser.Where(su => su.UserId == userId))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.VideoFiles.Where(v => v.Folder != null))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.VideoFiles.Where(v => v.Folder != null))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<Special?> GetSpecialPlaylistAsync(Guid userId, Ulid id, string language, string country, CancellationToken ct = default)
    {
        return context.Specials
            .AsNoTracking()
            .AsSplitQuery()
            .Where(special => special.Id == id)
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.Translations.Where(t => t.Iso6391 == language))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.Images.Where(i => i.Type == "logo").Take(1))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.Metadata)
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId && ud.Type == "specials"))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.MovieUser.Where(mu => mu.UserId == userId))
            .Include(special => special.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Translations.Where(t => t.Iso6391 == language))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Images.Where(i => i.Type == "logo").Take(1))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.Metadata)
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Tv)
                .ThenInclude(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Tv)
                .ThenInclude(tv => tv.Images.Where(i => i.Type == "logo").Take(1))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Tv)
                .ThenInclude(tv => tv.TvUser.Where(tu => tu.UserId == userId))
            .Include(special => special.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> AddToWatchListAsync(Ulid specialId, Guid userId, bool add = true, CancellationToken ct = default)
    {
        Special? special = await context.Specials
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == specialId, ct);

        if (special is null)
            return false;

        if (add)
        {
            // Find the first item in the special with a video file (prefer movies)
            SpecialItem? firstItemWithVideo = await context.SpecialItems
                .Where(si => si.SpecialId == specialId)
                .Include(si => si.Movie)
                    .ThenInclude(m => m!.VideoFiles)
                .Include(si => si.Episode)
                    .ThenInclude(e => e!.VideoFiles)
                .OrderBy(si => si.Order)
                .FirstOrDefaultAsync(ct);

            if (firstItemWithVideo is not null)
            {
                VideoFile? videoFile = firstItemWithVideo.Movie?.VideoFiles.FirstOrDefault(vf => vf.Folder != null)
                    ?? firstItemWithVideo.Episode?.VideoFiles.FirstOrDefault(vf => vf.Folder != null);

                if (videoFile is not null)
                {
                    // Check if userdata already exists for this video file
                    UserData? existingUserData = await context.UserData
                        .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.VideoFileId == videoFile.Id, ct);

                    if (existingUserData is null)
                    {
                        context.UserData.Add(new()
                        {
                            UserId = userId,
                            VideoFileId = videoFile.Id,
                            SpecialId = specialId,
                            Time = 0,
                            LastPlayedDate = DateTime.UtcNow.ToString("o"),
                            Type = Config.SpecialMediaType
                        });
                    }
                }
            }
        }
        else
        {
            // Remove all userdata for this special
            List<UserData> userDataToRemove = await context.UserData
                .Where(ud => ud.UserId == userId && ud.SpecialId == specialId)
                .ToListAsync(ct);

            context.UserData.RemoveRange(userDataToRemove);
        }

        await context.SaveChangesAsync(ct);
        return true;
    }
}
