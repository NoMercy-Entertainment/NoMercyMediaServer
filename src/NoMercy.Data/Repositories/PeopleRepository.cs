using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.People;

namespace NoMercy.Data.Repositories;

public class PeopleRepository(MediaContext context) : IPeopleRepository
{
    public Task<List<Person>> GetPeopleAsync(
        Guid userId,
        string language,
        int take,
        int page = 0,
        CancellationToken ct = default
    )
    {
        return context
            .People.AsNoTracking()
            .Where(person =>
                person.Casts.Any(cast =>
                    cast.Tv != null
                    && cast.Tv.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId))
                        != null
                )
                || person.Casts.Any(cast =>
                    cast.Movie != null
                    && cast.Movie.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId))
                        != null
                )
            )
            .Include(person =>
                person.Translations.Where(translation => translation.Iso6391 == language)
            )
            .OrderByDescending(person => person.Popularity)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<Person?> GetPersonWithCreditsAsync(int id, CancellationToken ct = default)
    {
        return context
            .People.AsNoTracking()
            .Where(person => person.Id == id)
            .Include(person => person.Casts)
                .ThenInclude(cast => cast.Movie)
                    .ThenInclude(movie => movie!.VideoFiles)
            .Include(person => person.Casts)
                .ThenInclude(cast => cast.Tv)
                    .ThenInclude(tv => tv!.Episodes)
                        .ThenInclude(episode => episode.VideoFiles)
            .Include(person => person.Crews)
                .ThenInclude(crew => crew.Movie)
                    .ThenInclude(movie => movie!.VideoFiles)
            .Include(person => person.Crews)
                .ThenInclude(crew => crew.Tv)
                    .ThenInclude(tv => tv!.Episodes)
                        .ThenInclude(episode => episode.VideoFiles)
            .FirstOrDefaultAsync(ct);
    }
}
