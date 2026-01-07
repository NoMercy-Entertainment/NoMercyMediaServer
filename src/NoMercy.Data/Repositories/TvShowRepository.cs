using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Other;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Data.Repositories;

public class TvShowRepository(MediaContext context)
{
    public Task<Tv?> GetTvAsync(Guid userId, int id, string language, string country)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(tv => tv.TvUser.Where(tu => tu.UserId == userId))
            .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Images.Where(i => i.Type == "logo").Take(1))
            .Include(tv => tv.CertificationTvs
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId))
            .FirstOrDefaultAsync();
    }

    public Task<Tv?> GetTvDetailAsync(Guid userId, int id, string language, string country)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(tv => tv.TvUser.Where(tu => tu.UserId == userId))
            .Include(tv => tv.Library)
                .ThenInclude(library => library.LibraryUsers)
            .Include(tv => tv.Media.Where(m => m.Type == "Trailer").Take(5))
            .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Images
                .Where(i => (i.Type == "logo" && i.Iso6391 == "en") ||
                            ((i.Type == "backdrop" || i.Type == "poster") && (i.Iso6391 == "en" || i.Iso6391 == null))))
            .Include(tv => tv.CertificationTvs
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country))
                .ThenInclude(c => c.Certification)
            .Include(tv => tv.Creators)
                .ThenInclude(c => c.Person)
            .Include(tv => tv.GenreTvs)
                .ThenInclude(g => g.Genre)
            .Include(tv => tv.KeywordTvs)
                .ThenInclude(k => k.Keyword)
            .Include(tv => tv.Cast.Take(20))
                .ThenInclude(c => c.Person)
            .Include(tv => tv.Cast)
                .ThenInclude(c => c.Role)
            .Include(tv => tv.Crew.Take(20))
                .ThenInclude(c => c.Person)
            .Include(tv => tv.Crew)
                .ThenInclude(c => c.Job)
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Episodes)
                .ThenInclude(e => e.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Episodes)
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId))
            .Include(tv => tv.RecommendationFrom)
            .Include(tv => tv.SimilarFrom)
            .Include(tv => tv.WatchProviderMedia.Where(w => w.CountryCode == country))
                .ThenInclude(w => w.WatchProvider)
            .Include(tv => tv.NetworkTvs)
                .ThenInclude(n => n.Network)
            .Include(tv => tv.CompaniesTvs)
                .ThenInclude(c => c.Company)
            .FirstOrDefaultAsync();
    }

    public Task<bool> GetTvAvailableAsync(Guid userId, int id)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(tv => tv.Id == id)
            .AnyAsync(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)));
    }

    public Task<Tv?> GetTvPlaylistAsync(Guid userId, int id, string language, string country)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Images.Where(i => i.Type == "logo").Take(1))
            .Include(tv => tv.Media.Where(m => m.Type == "video").Take(3))
            .Include(tv => tv.CertificationTvs
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Episodes)
                .ThenInclude(e => e.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Episodes)
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.Metadata)
            .Include(tv => tv.Seasons)
                .ThenInclude(s => s.Episodes)
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(v => v.UserData.Where(ud => ud.UserId == userId && ud.Type == "tv"))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> LikeTvAsync(int id, Guid userId, bool like)
    {
        TvUser? tvUser = await context.TvUser
            .FirstOrDefaultAsync(tu => tu.TvId == id && tu.UserId == userId);

        if (like)
        {
            await context.TvUser.Upsert(new(id, userId))
                .On(m => new { m.TvId, m.UserId })
                .WhenMatched(m => new()
                {
                    TvId = m.TvId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else if (tvUser != null)
        {
            context.TvUser.Remove(tvUser);
            await context.SaveChangesAsync();
        }

        return true;
    }

    public async Task AddTvShowAsync(int id)
    {
        TmdbTvClient tvClient = new(id);
        TmdbTvShowDetails? show = await tvClient.Details(true);
        if (show == null) return;

        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        Library? tvLibrary = await context.Libraries
            .Where(f => f.Type == (isAnime ? "anime" : "tv"))
            .FirstOrDefaultAsync() ?? await context.Libraries
            .Where(f => f.Type == "tv")
            .FirstOrDefaultAsync();

        if (tvLibrary == null) return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddShowJob>(id, tvLibrary);
    }

    public Task DeleteTvAsync(int id)
    {
        return context.Tvs
            .Where(tv => tv.Id == id)
            .ExecuteDeleteAsync();
    }

    public async Task<IEnumerable<Episode>> GetMissingLibraryShows(Guid userId, int id, string language)
    {
        Tv? tv = await context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(tv => tv.Episodes.Where(e => e.VideoFiles.Count == 0))
                .ThenInclude(e => e.Translations.Where(t => t.Iso6391 == language))
            .FirstOrDefaultAsync();

        if (tv == null)
            return [];

        return tv.Episodes.Where(e => e.Translations.Any());
    }
}
