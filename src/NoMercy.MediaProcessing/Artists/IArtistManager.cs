using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Artists;

public interface IArtistManager
{
    // Task StoreArtistAsync(MusicBrainzArtistAppends artist, Library library, Folder libraryFolder, MediaFolder mediaFolder, MusicBrainzReleaseAppends releaseAppends);

    Task Store(ReleaseArtistCredit artistCredit, Library library, Folder libraryFolder, MediaFolder mediaFolder,
        MusicBrainzReleaseAppends releaseAppends);

    // Task StoreArtist(MusicBrainzArtistDetails artistCredit, Library library, Folder libraryFolder,MediaFolder mediaFolder, MusicBrainzReleaseAppends releaseAppends);
}