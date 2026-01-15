using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public interface IReleaseGroupRepository
{
    public Task Store(ReleaseGroup releaseGroup);
}