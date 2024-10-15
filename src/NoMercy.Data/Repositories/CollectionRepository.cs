using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class CollectionRepository(MediaContext context) : ICollectionRepository
{
    public async Task<List<Collection>> GetCollectionsAsync(Guid userId, string language, int take, int page)
    {
        IOrderedQueryable<Collection> query = context.Collections
            .AsNoTracking()
            .Where(collection => collection.Library.LibraryUsers
                .Any(u => u.UserId == userId)
            )
            .Where(collection => collection.CollectionMovies
                .Any(collectionMovie => collectionMovie.Movie.VideoFiles.Count != 0)
            )
            .Include(collection => collection.Images)
            .Include(collection => collection.Translations
                .Where(translation => translation.Iso6391 == language))
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.Media)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.GenreMovies)
            .ThenInclude(genreMovie => genreMovie.Genre)
            .OrderBy(collection => collection.TitleSort);

        List<Collection>? collections = await query
            .Skip(page * take)
            .Take(take)
            .ToListAsync();

        return collections;
    }

    public Task<Collection?> GetCollectionAsync(Guid userId, int id, string? language, string country)
    {
        return context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .Where(collection => collection.Library.LibraryUsers
                .FirstOrDefault(u => u.UserId == userId) != null)
            .Include(collection => collection.CollectionUser
                .Where(x => x.UserId == userId)
            )
            .Include(collection => collection.Library)
            .ThenInclude(library => library.LibraryUsers)
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
                .Where(x => x.UserId == userId)
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
            .ThenInclude(movie => movie.Cast)
            .ThenInclude(genreMovie => genreMovie.Person)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.Cast)
            .ThenInclude(genreMovie => genreMovie.Role)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.Crew)
            .ThenInclude(genreMovie => genreMovie.Job)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.Crew)
            .ThenInclude(genreMovie => genreMovie.Person)
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
            .FirstOrDefaultAsync();
    }

    public Task<Collection?> GetAvailableCollectionAsync(Guid userId, int id)
    {
        return context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .Where(collection => collection.Library.LibraryUsers
                .FirstOrDefault(u => u.UserId == userId) != null)
            .Include(movie => movie.CollectionMovies)
            .Where(collectionMovie => collectionMovie.CollectionMovies
                .Any(movie => movie.Movie.VideoFiles.Any()))
            .Include(movie => movie.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(movieUser => movieUser.UserId == userId))
            .FirstOrDefaultAsync();
    }

    public Task<Collection?> GetWatchCollectionAsync(Guid userId, int id, string language, string country)
    {
        return context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .Include(collection =>
                collection.CollectionMovies.OrderBy(collectionMovie => collectionMovie.Movie.ReleaseDate))
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.Library.LibraryUsers)
            .Where(collection => collection.Library.LibraryUsers
                .FirstOrDefault(libraryUser => libraryUser.UserId == userId) != null)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.Media
                .Where(media => media.Type == "video"))
            .Include(collection => collection.Images
                .Where(image => image.Type == "logo"))
            .Include(collection => collection.Translations
                .Where(translation =>
                    translation.Iso6391 == language))
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.Images)
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.Translations
                .Where(translation => translation.Iso6391 == language)
            )
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(movieUser => movieUser.UserId == userId))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> LikeCollectionAsync(int id, Guid userId, bool like)
    {
        Collection? collection = await context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .FirstOrDefaultAsync();

        if (collection is null) return false;

        if (like)
        {
            await context.CollectionUser.Upsert(new CollectionUser(collection.Id, userId))
                .On(m => new { m.CollectionId, m.UserId })
                .WhenMatched(m => new CollectionUser
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
                    collectionUser.CollectionId == collection.Id && collectionUser.UserId == userId)
                .FirstOrDefaultAsync();

            if (collectionUser is not null) context.CollectionUser.Remove(collectionUser);

            await context.SaveChangesAsync();
        }

        return true;
    }
}