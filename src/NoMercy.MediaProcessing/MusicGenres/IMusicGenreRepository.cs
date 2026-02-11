using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.MediaProcessing.MusicGenres;

public interface IMusicGenreRepository
{
    public Task Store(MusicGenre musicGenre);
    public Task LinkToArtist(IEnumerable<ArtistMusicGenre> genreArtists);
    public Task LinkToRecording(IEnumerable<MusicGenreTrack> genreRecordings);
    public Task LinkToReleaseGroup(MusicGenreReleaseGroup genreReleaseGroup);
    public Task LinkToRelease(IEnumerable<AlbumMusicGenre> genreReleases);
}