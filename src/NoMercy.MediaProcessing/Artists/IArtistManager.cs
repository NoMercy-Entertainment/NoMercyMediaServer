using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
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