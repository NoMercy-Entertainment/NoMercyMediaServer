using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
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