using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.MediaProcessing.Artists;

public interface IArtistRepository
{
    public Task StoreAsync(Artist artist);
    Task LinkToRelease(AlbumArtist insert);
    Task LinkToLibrary(ArtistLibrary insert);
    Task LinkToReleaseGroup(ArtistReleaseGroup insert);
    Task LinkToRecording(ArtistTrack insert);
}