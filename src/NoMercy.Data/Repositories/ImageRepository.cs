using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public class ImageRepository(MediaContext context) : IImageRepository
{
    public Task<Image?> GetImageByFilePathAsync(string filePath, CancellationToken ct = default)
    {
        return context
            .Images.AsNoTracking()
            .FirstOrDefaultAsync(image => image.FilePath == filePath, ct);
    }
}
