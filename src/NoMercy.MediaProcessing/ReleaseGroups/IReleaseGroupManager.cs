using NoMercy.MediaProcessing.Images;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public interface IReleaseGroupManager
{
    public Task Store(MusicBrainzReleaseGroup releaseGroup, Ulid id,
        CoverArtImageManagerManager.CoverPalette colorPalette);
}