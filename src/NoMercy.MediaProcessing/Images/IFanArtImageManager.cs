using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Providers.FanArt.Models;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.MediaProcessing.Images;

public interface IFanArtImageManager
{
    Task<ICollection<Image>> StoreArtistImages(FanArtArtistDetails fanArtArtistDetails, Guid artistId, Artist dbArtist);
    Task StoreReleaseImages(FanArtAlbum fanArtAlbum, Guid albumId);
}