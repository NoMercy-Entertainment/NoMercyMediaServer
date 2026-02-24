using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Files;

public interface IFileRepository
{
    Task StoreVideoFile(VideoFile videoFile);
    Task<Ulid> StoreMetadata(Metadata metadata);
    Task<Episode?> GetEpisode(int? showId, MediaFile item);
    Task<(Movie? movie, Tv? show, string type)> MediaType(int id, Library library);
    Task<int> DeleteVideoFilesByHostFolderAsync(string hostFolder);
    Task<int> DeleteMetadataByHostFolderAsync(string hostFolder);
    Task<int> UpdateVideoFilePathsAsync(string oldHostFolder, string oldFilename, string newHostFolder, string newFilename);
    Task DeleteVideoFilesAndMetadataByMovieIdAsync(int movieId);
    Task DeleteVideoFilesAndMetadataByTvIdAsync(int tvId);
}