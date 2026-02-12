using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Recordings;

public interface IRecordingManager
{
    public Task<bool> Store(MusicBrainzReleaseAppends releaseAppends,
        MusicBrainzTrack musicBrainzTrack, MusicBrainzMedia musicBrainzMedia, Folder libraryFolder,
        MediaFolder mediaFolder, CoverArtImageManagerManager.CoverPalette? releaseCoverPalette);
}