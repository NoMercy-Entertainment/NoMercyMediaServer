using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class CollectionRepository(MediaContext context)
{
    public async Task<List<Collection>> GetCollectionsAsync(Guid userId, string language, int take, int page)
    {
        IOrderedQueryable<Collection> query = context.Collections
            .AsNoTracking()
            // .Where(collection => collection.Library.LibraryUsers
            //     .Any(u => u.UserId.Equals(userId))
            // )
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

        List<Collection> collections = await query
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
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
            .Include(collection => collection.CollectionUser
                .Where(x => x.UserId.Equals(userId))
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

    public IQueryable<Collection> GetCollectionItems(Guid userId, string? language, int take = 1, int page = 1,
        Expression<Func<Collection, object>>? orderByExpression = null, string? direction = null)
    {
        IIncludableQueryable<Collection, IEnumerable<Image>> x = context.Collections
            .AsNoTracking()
            .Where(collection => collection.Library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
            .Where(collection => collection.CollectionMovies
                .Any(collectionMovie => collectionMovie.Movie.VideoFiles.Count != 0)
            )
            .Include(collection => collection.CollectionUser
                .Where(x => x.UserId.Equals(userId))
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
                .Where(x => x.UserId.Equals(userId))
            )
            .Include(collection => collection.CollectionMovies)
            .ThenInclude(movie => movie.Movie)
            .ThenInclude(movie => movie.CertificationMovies
                .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US" ||
                                             certificationMovie.Certification.Iso31661 == "NL")
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
                ));

        if (orderByExpression is not null && direction == "desc")
        {
            return x.OrderByDescending(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }
        if (orderByExpression is not null)
        {
            return x.OrderBy(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }

        return x.OrderBy(tv => tv.TitleSort)
            .Skip(page * take)
            .Take(take);
    }

    public Task<Collection?> GetAvailableCollectionAsync(Guid userId, int id)
    {
        return context.Collections
            .AsNoTracking()
            .Where(collection => collection.Id == id)
            .Where(collection => collection.Library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
            .Include(movie => movie.CollectionMovies)
            .Where(collectionMovie => collectionMovie.CollectionMovies
                .Any(movie => movie.Movie.VideoFiles.Any()))
            .Include(movie => movie.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(movieUser => movieUser.UserId.Equals(userId)))
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
                .FirstOrDefault(libraryUser => libraryUser.UserId.Equals(userId)) != null)
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
                .Where(movieUser => movieUser.UserId.Equals(userId)))
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
                .FirstOrDefaultAsync();

            if (collectionUser is not null) context.CollectionUser.Remove(collectionUser);

            await context.SaveChangesAsync();
        }

        return true;
    }
}
