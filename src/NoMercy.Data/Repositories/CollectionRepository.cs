using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class CollectionListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? TranslatedTitle { get; set; }
    public string? TranslatedOverview { get; set; }
    public string? Overview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public IColorPalettes ColorPalette { get; set; } = null!;
    public string? Logo { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? FirstMovieYear { get; set; }
    public int TotalMovies { get; set; }
    public int MoviesWithVideo { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
}

public class CollectionRepository(MediaContext context)
{
    public async Task<List<Collection>> GetCollectionsAsync(Guid userId, string language, int take, int page, CancellationToken ct = default)
    {
        List<Collection> collections = await context.Collections
            .AsNoTracking()
            .ForUser(userId)
            .Where(collection => collection.CollectionMovies.Any(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)))
            .Include(collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(collection => collection.Images.Where(i => i.Type == "logo").Take(1))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.CertificationMovies.Where(cert => cert.Certification.Iso31661 == "US").Take(1))
                .ThenInclude(cert => cert.Certification)
            .OrderBy(collection => collection.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);

        return collections;
    }

    public async Task<List<CollectionListDto>> GetCollectionsListAsync(Guid userId, string language, string country, int take, int page, CancellationToken ct = default)
    {
        return await context.Collections
            .AsNoTracking()
            .ForUser(userId)
            .Where(collection => collection.CollectionMovies.Any(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)))
            .OrderBy(collection => collection.TitleSort)
            .Skip(page * take)
            .Take(take)
            .Select(collection => new CollectionListDto
            {
                Id = collection.Id,
                Title = collection.Title,
                TitleSort = collection.TitleSort ?? collection.Title,
                TranslatedTitle = collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                    ? collection.Translations.First(t => t.Iso6391 == language).Title
                    : null,
                TranslatedOverview = collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                    ? collection.Translations.First(t => t.Iso6391 == language).Overview
                    : null,
                Overview = collection.Overview,
                ColorPalette = collection.ColorPalette!,
                Poster = collection.Poster,
                Backdrop = collection.Backdrop,
                Logo = collection.Images.FirstOrDefault(i => i.Type == "logo") != null
                    ? collection.Images.First(i => i.Type == "logo").FilePath
                    : null,
                CreatedAt = collection.CreatedAt,
                FirstMovieYear = collection.CollectionMovies
                    .Where(cm => cm.Movie.ReleaseDate != null)
                    .OrderBy(cm => cm.Movie.ReleaseDate)
                    .Select(cm => cm.Movie.ReleaseDate!.Value.Year)
                    .FirstOrDefault(),
                TotalMovies = collection.CollectionMovies.Count,
                MoviesWithVideo = collection.CollectionMovies.Count(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)),
                CertificationRating = collection.CollectionMovies
                    .SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country)
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = collection.CollectionMovies
                    .SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country)
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    // public Task<Collection?> GetCollectionAsync(Guid userId, int id, string? language, string country)
    // {
    //     return context.Collections
    //         .AsNoTracking()
    //         .Where(collection => collection.Id == id)
    //         .Where(collection => collection.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Include(collection => collection.CollectionUser.Where(x => x.UserId == userId))
    //         .Include(collection => collection.Translations.Where(t => t.Iso6391 == language))
    //         .Include(collection => collection.Images.Where(i => i.Type == "logo").Take(1))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.Translations.Where(t => t.Iso6391 == language))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.MovieUser.Where(x => x.UserId == userId))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.CertificationMovies
    //                 .Where(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
    //                 .Take(1))
    //             .ThenInclude(cert => cert.Certification)
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.Images.Where(i => i.Type == "logo").Take(1))
    //         .FirstOrDefaultAsync();
    // }
    
    public async Task<Collection?> GetCollectionAsync(Guid userId, int id, string? language, string country, CancellationToken ct = default)
    {
        // Query 1: Core collection data — metadata, translations, images
        // Removed: Library.LibraryUsers Include (only needed in WHERE clause, not consumed by DTO)
        // Movie cast/crew split to Query 2 to reduce round-trips
        Collection? collection = await context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .ForUser(userId)
            .Include(collection => collection.CollectionUser
                .Where(x => x.UserId.Equals(userId))
            )
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.Translations
                .Where(translation => translation.Iso6391 == language))
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.MovieUser
                .Where(x => x.UserId.Equals(userId))
            )
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.CertificationMovies
                .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US" ||
                                             certificationMovie.Certification.Iso31661 == country)
            )
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.GenreMovies)
            .ThenInclude(genreMovie => genreMovie.Genre)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.Images
                .Where(image =>
                    (image.Type == "logo" && image.Iso6391 == "en")
                    || ((image.Type == "backdrop" || image.Type == "poster") &&
                        (image.Iso6391 == "en" || image.Iso6391 == null))
                )
                .OrderByDescending(image => image.VoteAverage)
                .Take(30)
            )
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.KeywordMovies)
                .ThenInclude(keywordMovie => keywordMovie.Keyword)
            .Include(collection => collection.Translations
                .Where(translation => translation.Iso6391 == language))
            .Include(collection => collection.Images
                .Where(image =>
                    (image.Type == "logo" && image.Iso6391 == "en")
                    || ((image.Type == "backdrop" || image.Type == "poster") &&
                        (image.Iso6391 == "en" || image.Iso6391 == null))
                )
                .OrderByDescending(image => image.VoteAverage)
            )
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (collection is null) return null;

        // Query 2: Movie-level cast/crew — loaded separately to reduce query complexity
        List<int> movieIds = collection.CollectionMovies.Select(cm => cm.MovieId).ToList();
        List<Movie> moviesWithCastCrew = await context.Movies.AsNoTracking()
            .Where(m => movieIds.Contains(m.Id))
            .Include(m => m.Cast)
                .ThenInclude(c => c.Person)
            .Include(m => m.Cast)
                .ThenInclude(c => c.Role)
            .Include(m => m.Crew)
                .ThenInclude(c => c.Person)
            .Include(m => m.Crew)
                .ThenInclude(c => c.Job)
            .AsSplitQuery()
            .ToListAsync(ct);

        // Merge movie cast/crew into the main query results
        Dictionary<int, Movie> movieLookup = moviesWithCastCrew.ToDictionary(m => m.Id);
        foreach (CollectionMovie cm in collection.CollectionMovies)
        {
            if (movieLookup.TryGetValue(cm.MovieId, out Movie? loaded))
            {
                cm.Movie.Cast = loaded.Cast;
                cm.Movie.Crew = loaded.Crew;
            }
        }

        return collection;
    }

    public Task<List<CollectionListDto>> GetCollectionItemCardsAsync(Guid userId, string? language, string country, int take = 1, int page = 0, CancellationToken ct = default)
    {
        return context.Collections
            .AsNoTracking()
            .ForUser(userId)
            .Where(collection => collection.CollectionMovies.Any(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)))
            .OrderBy(collection => collection.TitleSort)
            .Skip(page * take)
            .Take(take)
            .Select(collection => new CollectionListDto
            {
                Id = collection.Id,
                Title = collection.Title,
                TitleSort = collection.TitleSort ?? collection.Title,
                TranslatedTitle = collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                    ? collection.Translations.First(t => t.Iso6391 == language).Title
                    : null,
                TranslatedOverview = collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                    ? collection.Translations.First(t => t.Iso6391 == language).Overview
                    : null,
                Overview = collection.Overview,
                ColorPalette = collection.ColorPalette!,
                Poster = collection.Poster,
                Backdrop = collection.Backdrop,
                Logo = collection.Images.FirstOrDefault(i => i.Type == "logo") != null
                    ? collection.Images.First(i => i.Type == "logo").FilePath
                    : null,
                CreatedAt = collection.CreatedAt,
                FirstMovieYear = collection.CollectionMovies
                    .Where(cm => cm.Movie.ReleaseDate != null)
                    .OrderBy(cm => cm.Movie.ReleaseDate)
                    .Select(cm => cm.Movie.ReleaseDate!.Value.Year)
                    .FirstOrDefault(),
                TotalMovies = collection.CollectionMovies.Count,
                MoviesWithVideo = collection.CollectionMovies.Count(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)),
                CertificationRating = collection.CollectionMovies
                    .SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country)
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = collection.CollectionMovies
                    .SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm => cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country)
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public Task<List<Collection>> GetCollectionItems(Guid userId, string? language, string country, int take = 1, int page = 0, CancellationToken ct = default)
    {
        return context.Collections
            .AsNoTracking()
            .AsSplitQuery()
            .ForUser(userId)
            .Where(collection => collection.CollectionMovies.Any(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)))
            .Include(collection => collection.CollectionUser.Where(x => x.UserId == userId))
            .Include(collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(collection => collection.Images.Where(i => i.Type == "logo").Take(1))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.CertificationMovies
                    .Where(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(cert => cert.Certification)
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.Images.Where(i => i.Type == "logo").Take(1))
            .OrderBy(c => c.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<Collection?> GetAvailableCollectionAsync(Guid userId, int id, CancellationToken ct = default)
    {
        return context.Collections
            .AsNoTracking()
            .AsSplitQuery()
            .Where(collection => collection.Id == id)
            .ForUser(userId)
            .Where(collection => collection.CollectionMovies.Any(cm => cm.Movie.VideoFiles.Any(v => v.Folder != null)))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.Metadata)
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId))
            .FirstOrDefaultAsync(ct);
    }

    public Task<Collection?> GetCollectionPlaylistAsync(Guid userId, int id, string language, string country, CancellationToken ct = default)
    {
        return context.Collections
            .AsNoTracking()
            .AsSplitQuery()
            .Where(collection => collection.Id == id)
            .ForUser(userId)
            .Include(collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(collection => collection.Images.Where(i => i.Type == "logo").Take(1))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.Images.Where(i => i.Type == "logo").Take(1))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.Media.Where(media => media.Type == "video"))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.Metadata)
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId && ud.Type == "collection"))
            .Include(collection => collection.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.CertificationMovies
                    .Where(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(cert => cert.Certification)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> LikeAsync(int id, Guid userId, bool like, CancellationToken ct = default)
    {
        Collection? collection = await context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .FirstOrDefaultAsync(ct);

        if (collection is null) return false;

        if (like)
        {
            await context.CollectionUser.Upsert(new(collection.Id, userId))
                .On(m => new { m.CollectionId, m.UserId })
                .WhenMatched(m => new()
                {
                    CollectionId = m.CollectionId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            CollectionUser? collectionUser = await context.CollectionUser
                .Where(collectionUser =>
                    collectionUser.CollectionId == collection.Id && collectionUser.UserId.Equals(userId))
                .FirstOrDefaultAsync(ct);

            if (collectionUser is not null) context.CollectionUser.Remove(collectionUser);

            await context.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> AddToWatchListAsync(int collectionId, Guid userId, bool add = true, CancellationToken ct = default)
    {
        Collection? collection = await context.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection is null)
            return false;
    
        if (add)
        {
            // Find the first movie in the collection with a video file
            CollectionMovie? firstMovieWithVideo = await context.CollectionMovie
                .Where(cm => cm.CollectionId == collectionId)
                .Include(cm => cm.Movie)
                    .ThenInclude(m => m.VideoFiles)
                .OrderBy(cm => cm.Movie.TitleSort)
                .FirstOrDefaultAsync(ct);
    
            if (firstMovieWithVideo?.Movie?.VideoFiles.FirstOrDefault(vf => vf.Folder != null) is { } videoFile)
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
                        CollectionId = collectionId,
                        Time = 0,
                        LastPlayedDate = DateTime.UtcNow.ToString("o"),
                        Type = Config.CollectionMediaType
                    });
                }
            }
        }
        else
        {
            // Remove all userdata for this collection
            List<UserData> userDataToRemove = await context.UserData
                .Where(ud => ud.UserId == userId && ud.CollectionId == collectionId)
                .ToListAsync(ct);
    
            context.UserData.RemoveRange(userDataToRemove);
        }
    
        await context.SaveChangesAsync(ct);
        return true;
    }
    
    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        return context.Collections
            .Where(collection => collection.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}