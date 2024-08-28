using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public class ReleaseGroupRepository(MediaContext context) : IReleaseGroupRepository
{
    public Task StoreAsync(ReleaseGroup releaseGroup)
    {
        return context.ReleaseGroups.Upsert(releaseGroup)
            .On(e => new { e.Id })
            .WhenMatched((s, i) => new ReleaseGroup
            {
                UpdatedAt = DateTime.UtcNow,
                Id = i.Id,
                Title = i.Title,
                Description = i.Description,
                Year = i.Year,
                LibraryId = i.LibraryId
            })
            .RunAsync();
    }
}