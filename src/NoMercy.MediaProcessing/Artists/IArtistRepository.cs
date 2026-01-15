using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Artists;

public interface IArtistRepository
{
    public Task StoreAsync(Artist artist);
    Task LinkToRelease(AlbumArtist insert);
    Task LinkToLibrary(ArtistLibrary insert);
    Task LinkToReleaseGroup(ArtistReleaseGroup insert);
    Task LinkToRecording(ArtistTrack insert);
}