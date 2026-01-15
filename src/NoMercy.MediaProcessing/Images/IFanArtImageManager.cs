using NoMercy.Database.Models;
using NoMercy.Providers.FanArt.Models;
using Image = NoMercy.Database.Models.Image;

namespace NoMercy.MediaProcessing.Images;

public interface IFanArtImageManager
{
    Task<ICollection<Image>> StoreArtistImages(FanArtArtistDetails fanArtArtistDetails, Guid artistId, Artist dbArtist);
    Task StoreReleaseImages(FanArtAlbum fanArtAlbum, Guid albumId);
}