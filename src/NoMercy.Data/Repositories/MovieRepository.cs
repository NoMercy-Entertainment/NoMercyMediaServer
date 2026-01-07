using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Data.Repositories;

public class MovieRepository(MediaContext context)
{
    public Task<Movie?> GetMovieAsync(Guid userId, int id, string language, string country)
    {
        return context.Movies
            .AsNoTracking()
            .Where(movie => movie.Id == id)
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(movie => movie.MovieUser.Where(mu => mu.UserId == userId))
            .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(movie => movie.Images.Where(i => i.Type == "logo").Take(1))
            .Include(movie => movie.CertificationMovies
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId))
            .FirstOrDefaultAsync();
    }

    public Task<Movie?> GetMovieDetailAsync(Guid userId, int id, string language, string country)
    {
        return context.Movies
            .AsNoTracking()
            .Where(movie => movie.Id == id)
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(movie => movie.MovieUser.Where(mu => mu.UserId == userId))
            .Include(movie => movie.Library)
                .ThenInclude(library => library.LibraryUsers)
            .Include(movie => movie.Media.Where(m => m.Type == "Trailer").Take(5))
            .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(movie => movie.Images
                .Where(i => (i.Type == "logo" && i.Iso6391 == "en") ||
                            ((i.Type == "backdrop" || i.Type == "poster") && (i.Iso6391 == "en" || i.Iso6391 == null))))
            .Include(movie => movie.CertificationMovies
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country))
                .ThenInclude(c => c.Certification)
            .Include(movie => movie.GenreMovies)
                .ThenInclude(g => g.Genre)
            .Include(movie => movie.KeywordMovies)
                .ThenInclude(k => k.Keyword)
            .Include(movie => movie.Cast.Take(20))
                .ThenInclude(c => c.Person)
            .Include(movie => movie.Cast)
                .ThenInclude(c => c.Role)
            .Include(movie => movie.Crew.Take(20))
                .ThenInclude(c => c.Person)
            .Include(movie => movie.Crew)
                .ThenInclude(c => c.Job)
            .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId))
            .Include(movie => movie.RecommendationFrom)
            .Include(movie => movie.SimilarFrom)
            .Include(movie => movie.WatchProviderMedia.Where(w => w.CountryCode == country))
                .ThenInclude(w => w.WatchProvider)
            .Include(movie => movie.CompaniesMovies)
                .ThenInclude(c => c.Company)
            .FirstOrDefaultAsync();
    }

    public Task<bool> GetMovieAvailableAsync(Guid userId, int id)
    {
        return context.Movies
            .AsNoTracking()
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(movie => movie.Id == id)
            .AnyAsync(movie => movie.VideoFiles.Any(v => v.Folder != null));
    }

    public Task<List<Movie>> GetMoviePlaylistAsync(Guid userId, int id, string language, string country)
    {
        return context.Movies
            .AsNoTracking()
            .Where(movie => movie.Id == id)
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(movie => movie.Images.Where(i => i.Type == "logo").Take(1))
            .Include(movie => movie.Media.Where(m => m.Type == "video").Take(3))
            .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.Metadata)
            .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId && ud.Type == "movie"))
            .Include(movie => movie.CertificationMovies
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .ToListAsync();
    }

    public async Task<bool> LikeMovieAsync(int id, Guid userId, bool like)
    {
        try
        {
            MovieUser? movieUser = await context.MovieUser
                .FirstOrDefaultAsync(mu => mu.MovieId == id && mu.UserId == userId);

            if (like)
            {
                await context.MovieUser.Upsert(new(id, userId))
                    .On(m => new { m.MovieId, m.UserId })
                    .WhenMatched(m => new()
                    {
                        MovieId = m.MovieId,
                        UserId = m.UserId
                    })
                    .RunAsync();
            }
            else if (movieUser != null)
            {
                context.MovieUser.Remove(movieUser);
                await context.SaveChangesAsync();
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    public async Task AddMovieAsync(int id)
    {
        Library? movieLibrary = await context.Libraries
            .Where(f => f.Type == "movie")
            .FirstOrDefaultAsync();

        if (movieLibrary == null) return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddMovieJob>(id, movieLibrary.Id);
    }

    public Task DeleteMovieAsync(int id)
    {
        return context.Movies
            .Where(movie => movie.Id == id)
            .ExecuteDeleteAsync();
    }
}
