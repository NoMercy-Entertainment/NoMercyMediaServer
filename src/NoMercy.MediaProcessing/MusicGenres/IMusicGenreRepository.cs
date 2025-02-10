using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.MusicGenres;

public interface IMusicGenreRepository
{
    public Task Store(MusicGenre musicGenre);
    public Task LinkToArtist(IEnumerable<ArtistMusicGenre> genreArtists);
    public Task LinkToRecording(IEnumerable<MusicGenreTrack> genreRecordings);
    public Task LinkToReleaseGroup(MusicGenreReleaseGroup genreReleaseGroup);
    public Task LinkToRelease(IEnumerable<AlbumMusicGenre> genreReleases);
}