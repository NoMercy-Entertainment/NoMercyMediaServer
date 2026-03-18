using NoMercy.Database.Models.Music;
using NoMercy.Providers.FanArt.Models;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.MediaProcessing.Images;

public interface IFanArtImageManager
{
    Task<ICollection<Image>> StoreArtistImages(FanArtArtistDetails fanArtArtistDetails, Guid artistId, Artist dbArtist);
    Task StoreReleaseImages(FanArtAlbum fanArtAlbum, Guid albumId);
}