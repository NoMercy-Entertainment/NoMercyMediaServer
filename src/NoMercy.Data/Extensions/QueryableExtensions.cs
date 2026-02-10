using NoMercy.Database.Models;

namespace NoMercy.Data.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Filters entities to only those belonging to libraries the user has access to.
    /// Works with any entity that has a Library navigation property (Movie, Tv, Collection, Album).
    /// </summary>
    public static IQueryable<T> ForUser<T>(this IQueryable<T> query, Guid userId) where T : class, IHasLibrary
    {
        return query.Where(entity => entity.Library.LibraryUsers.Any(u => u.UserId == userId));
    }

    /// <summary>
    /// Filters libraries to only those the user has access to.
    /// </summary>
    public static IQueryable<Library> ForUser(this IQueryable<Library> query, Guid userId)
    {
        return query.Where(library => library.LibraryUsers.Any(u => u.UserId == userId));
    }

    /// <summary>
    /// Filters artists to only those belonging to libraries the user has access to.
    /// Artist has a nullable LibraryId, so it needs a separate overload.
    /// </summary>
    public static IQueryable<Artist> ForUser(this IQueryable<Artist> query, Guid userId)
    {
        return query.Where(artist => artist.Library.LibraryUsers.Any(u => u.UserId == userId));
    }
}
