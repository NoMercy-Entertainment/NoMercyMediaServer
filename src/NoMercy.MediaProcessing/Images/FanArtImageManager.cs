using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using Serilog.Events;
using Image = NoMercy.Database.Models.Image;

namespace NoMercy.MediaProcessing.Images;

public class FanArtImageManager(
    ImageRepository imageRepository,
    JobDispatcher jobDispatcher
) : IFanArtImageManager
{
    public static async Task<string> ColorPalette(string type, Uri url, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(FanArtImageClient.Download, type, url, download);
    }

    public async Task<string> MultiColorPalette(IEnumerable<BaseImageManager.MultiUriType> items, bool? download = true)
    {
        return await BaseImageManager.MultiColorPalette(FanArtImageClient.Download, items, download);
    }
    
    public async Task<Image?> StoreArtistImages(FanArtArtistDetails fanArtArtistDetails, Guid artistId, Artist dbArtist)
    {
        try
        {
            List<Image> thumbs = fanArtArtistDetails.Thumbs.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "thumb",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = artistId,
                    Site = image.Url.BasePath(),
                    _colorPalette = ColorPalette("image", image.Url).Result
                });
            List<Image> logos = fanArtArtistDetails.Logos.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "logo",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = artistId,
                    Site = image.Url.BasePath(),
                    _colorPalette = ColorPalette("image", image.Url).Result
                });
            List<Image> banners = fanArtArtistDetails.Banners.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "banner",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = artistId,
                    Site = image.Url.BasePath(),
                    _colorPalette = ColorPalette("image", image.Url).Result
                });
            List<Image> hdLogos = fanArtArtistDetails.HdLogos.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "hdLogo",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = artistId,
                    Site = image.Url.BasePath(),
                    _colorPalette = ColorPalette("image", image.Url).Result
                });
            List<Image> artistBackgrounds = fanArtArtistDetails.Backgrounds.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "background",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = artistId,
                    Site = image.Url.BasePath(),
                    _colorPalette = ColorPalette("image", image.Url).Result
                });

            List<Image> images = thumbs
                .Concat(logos)
                .Concat(banners)
                .Concat(hdLogos)
                .Concat(artistBackgrounds)
                .ToList();

            await imageRepository.StoreArtistImages(images, dbArtist);

            return thumbs.FirstOrDefault();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return  null;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }
        
        return null;
    }
    
    public async Task StoreReleaseImages(FanArtAlbum fanArt, Guid releaseId)
    {
        try
        {
            List<Image> covers = [];
            List<Image> cdArts = [];
            foreach ((Guid _, Albums albums) in fanArt.Albums)
            {
                covers.AddRange(albums.Cover
                    .Select(image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cover",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        AlbumId = releaseId,
                        Site = image.Url.BasePath(),
                        Name = fanArt.Name,
                        _colorPalette = ColorPalette("image", image.Url).Result
                    }));

                cdArts.AddRange(albums.CdArt
                    .Select(image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cdArt",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        AlbumId = releaseId,
                        Site = image.Url.BasePath(),
                        Name = fanArt.Name,
                        _colorPalette = ColorPalette("image", image.Url).Result
                    }));
            }
            
            ReleaseGroup dbRelease = await imageRepository
                .GetReleaseImages(releaseId);

            IEnumerable<Image> images = covers
                .Concat(cdArts)
                .Where(image => dbRelease.AlbumReleaseGroup
                    .Any(ar => ar.AlbumId == image.AlbumId));

            Image? albumCover = covers.FirstOrDefault();

            dbRelease.Cover = albumCover?.FilePath ?? dbRelease.Cover;
            dbRelease._colorPalette = albumCover?._colorPalette.Replace("\"image\"", "\"cover\"")
                                      ?? dbRelease._colorPalette;

            foreach (AlbumReleaseGroup albumRelease in dbRelease.AlbumReleaseGroup)
            {
                albumRelease.Album.Cover = albumCover?.FilePath ?? albumRelease.Album.Cover;
                albumRelease.Album._colorPalette = albumCover?._colorPalette.Replace("\"image\"", "\"cover\"")
                                                   ?? albumRelease.Album._colorPalette;
            }
            
            await imageRepository.CommitReleaseChanges();
            await imageRepository.StoreReleaseImages(images);
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }
    }

    public async Task StoreReleaseImages(Dictionary<Guid, Albums> fanArtArtistAlbums, Guid artistId, Artist dbArtist)
    {
        // foreach ((Guid id,  var fanArtArtistAlbum) in fanArtArtistAlbums)
        // {
        //     await StoreReleaseImages(fanArtArtistAlbum, artistId, dbArtist);
        // }

        await Task.CompletedTask;
    }

    private async Task StoreReleaseImages(Albums fanArtArtistAlbums, Guid albumId, Artist dbArtist)
    {
          try
          {
                List<Image> cdArts = fanArtArtistAlbums.CdArt
                    .Select(image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cdArt",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        AlbumId = albumId,
                        Site = image.Url.BasePath(),
                        _colorPalette = ColorPalette("image", image.Url).Result
                    })
                    .ToList();
                
                await imageRepository.StoreArtistImages(cdArts, dbArtist);
                
                List<Image> covers = fanArtArtistAlbums.Cover
                    .Select(image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cover",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        AlbumId = albumId,
                        Site = image.Url.BasePath(),
                        _colorPalette = ColorPalette("image", image.Url).Result
                    })
                    .ToList();
                
                await imageRepository.StoreArtistImages(covers, dbArtist);
          }
          catch (Exception e)
          {
            if (e.Message.Contains("404")) return;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
          }   
    }
}