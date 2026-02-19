using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.MusicGenres;

public interface IMusicGenreManager
{
    public Task Store(MusicBrainzGenreDetails genre);
}