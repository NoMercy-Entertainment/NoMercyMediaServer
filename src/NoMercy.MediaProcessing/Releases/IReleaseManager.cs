using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Releases;

public interface IReleaseManager
{
    public Task<(MusicBrainzReleaseAppends? releaseAppends, CoverArtImageManagerManager.CoverPalette? coverPalette)>
        Add(Guid id, Library albumLibrary, Folder libraryFolder,
            MediaFolder mediaFolder);
}