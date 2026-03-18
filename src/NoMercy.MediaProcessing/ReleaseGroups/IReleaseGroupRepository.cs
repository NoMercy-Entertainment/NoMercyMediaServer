using NoMercy.Database.Models.Music;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public interface IReleaseGroupRepository
{
    public Task Store(ReleaseGroup releaseGroup);
}