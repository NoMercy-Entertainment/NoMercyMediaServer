using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class SpecialRepository(MediaContext context)
{
    public async Task<List<Special>> GetSpecialsAsync(Guid userId, string language, int take, int page)
    {
        List<Special> specials = await context.Specials
            .AsNoTracking()
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
            .ToListAsync();

        return specials;
    }
    
    public Task<Special?> GetSpecialAsync(Guid userId, Ulid id)
    {
        return Task.FromResult(context.Specials
            .AsNoTracking()
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

    public Task<List<Special>> GetSpecialItems(Guid userId, string? language, string country, int take = 1, int page = 0)
    {
        return context.Specials
            .AsNoTracking()
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
            .ToListAsync();
    }

    public Task<Special?> GetSpecialPlaylistAsync(Guid userId, Ulid id, string language, string country)
    {
        return context.Specials
            .AsNoTracking()
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
            .FirstOrDefaultAsync();
    }
}
