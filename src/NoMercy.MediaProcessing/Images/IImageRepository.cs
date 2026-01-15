using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Images;

public interface IImageRepository
{
    public Task<ICollection<Image>> StoreArtistImages(IEnumerable<Image> images, Artist dbArtist);
}