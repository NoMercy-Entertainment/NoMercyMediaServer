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

namespace NoMercy.MediaProcessing.ReleaseGroups;

public class ReleaseGroupRepository(MediaContext context) : IReleaseGroupRepository
{
    public Task Store(ReleaseGroup releaseGroup)
    {
        return context.ReleaseGroups.Upsert(releaseGroup)
            .On(e => new { e.Id })
            .WhenMatched((s, i) => new()
            {
                Id = i.Id,
                Title = i.Title,
                Description = i.Description,
                Year = i.Year,
                LibraryId = i.LibraryId,
                Cover = i.Cover,
            })
            .RunAsync();
    }
}