using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Recordings;

public interface IRecordingRepository
{
    Task Store(Track recording, bool update = false);
    Task LinkToRelease(AlbumTrack trackRelease);
    Task LinkToArtist(ArtistTrack insert);
    Task LinkToLibrary(LibraryTrack libraryTrack);
    Task LinkToLibrary(ArtistLibrary artistLibrary);
}