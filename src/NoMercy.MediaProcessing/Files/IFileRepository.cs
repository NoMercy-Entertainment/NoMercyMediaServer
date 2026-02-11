using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Files;

public interface IFileRepository
{
    Task StoreVideoFile(VideoFile videoFile);
    Task<Ulid> StoreMetadata(Metadata metadata);
    Task<Episode?> GetEpisode(int? showId, MediaFile item);
    Task<(Movie? movie, Tv? show, string type)> MediaType(int id, Library library);
}