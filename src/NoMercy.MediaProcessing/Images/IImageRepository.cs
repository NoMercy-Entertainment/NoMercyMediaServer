using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;

namespace NoMercy.MediaProcessing.Images;

public interface IImageRepository
{
    public Task<ICollection<Image>> StoreArtistImages(IEnumerable<Image> images, Artist dbArtist);
}