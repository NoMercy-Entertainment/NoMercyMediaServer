using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public class EncoderRepository(MediaContext context)
{
    public Task<List<EncoderProfile>> GetEncoderProfilesAsync()
    {
        return context.EncoderProfiles
            .ToListAsync();
    }

    public Task<EncoderProfile?> GetEncoderProfileByIdAsync(Ulid id)
    {
        return context.EncoderProfiles
            .FirstOrDefaultAsync(profile => profile.Id == id);
    }

    public Task AddEncoderProfileAsync(EncoderProfile profile)
    {
        return context.EncoderProfiles.Upsert(profile)
            .On(l => new { l.Id })
            .WhenMatched((ls, li) => new()
            {
                Id = li.Id,
                Name = li.Name,
                Container = li.Container,
                Param = li.Param
            })
            .RunAsync();
    }

    public Task DeleteEncoderProfileAsync(EncoderProfile profile)
    {
        context.EncoderProfiles
            .Remove(profile);

        return context.SaveChangesAsync();
    }

    public Task<int> GetEncoderProfileCountAsync()
    {
        return context.EncoderProfiles
            .CountAsync();
    }
}