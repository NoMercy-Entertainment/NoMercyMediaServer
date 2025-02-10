using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.MusicGenres;

public class MusicGenreManager() : BaseManager, IMusicGenreManager
{
    public Task Store(MusicBrainzGenreDetails genre)
    {
        throw new NotImplementedException();
    }
}