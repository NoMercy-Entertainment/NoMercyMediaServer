using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public interface IImageRepository
{
    Task<Image?> GetImageByFilePathAsync(string filePath, CancellationToken ct = default);
}
