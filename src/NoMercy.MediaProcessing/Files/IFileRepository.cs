using NoMercy.Database.Models;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Files;

public interface IFileRepository
{
    Task StoreVideoFile(VideoFile videoFile);
    Task<Ulid> StoreMetadata(Metadata metadata);
    Task<Episode?> GetEpisode(int? showId, MediaFile item);
    Task<(Movie? movie, Tv? show, string type)> MediaType(int id, Library library);
}