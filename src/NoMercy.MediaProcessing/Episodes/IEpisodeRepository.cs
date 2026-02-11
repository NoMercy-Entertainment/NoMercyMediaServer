using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.MediaProcessing.Episodes;

public interface IEpisodeRepository
{
    public Task StoreEpisodes(IEnumerable<Episode> episodes);
    public Task StoreEpisodeTranslations(List<Translation> translations);
    public Task StoreEpisodeImages(IEnumerable<Image> images);
}