using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.MusicGenres;

public class MusicGenreManager() : BaseManager, IMusicGenreManager
{
    private readonly MusicGenreRepository _musicGenreRepository = null!;
    public MusicGenreManager(MusicGenreRepository musicGenreRepository) : this()
    {
        _musicGenreRepository = musicGenreRepository;
    }

    public Task Store(MusicBrainzGenreDetails genre)
    {
        MusicGenre insert = new()
        {
            Id = genre.Id,
            Name = genre.Name
        };

        return _musicGenreRepository!.Store(insert);
    }
}