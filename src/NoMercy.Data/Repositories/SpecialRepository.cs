using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class SpecialRepository(MediaContext context)
{
    public async Task<List<Special>> GetSpecialsAsync(Guid userId, string language, int take, int page, CancellationToken ct = default)
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
            .ToListAsync(ct);

        return specials;
    }

    public Task<Special?> GetSpecialAsync(Guid userId, Ulid id, CancellationToken ct = default)
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

    public Task<List<Special>> GetSpecialItems(Guid userId, string? language, string country, int take = 1, int page = 0, CancellationToken ct = default)
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
            .ToListAsync(ct);
    }

    public Task<Special?> GetSpecialPlaylistAsync(Guid userId, Ulid id, string language, string country, CancellationToken ct = default)
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
