using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using Serilog.Events;
using Image = NoMercy.Database.Models.Image;

namespace NoMercy.MediaProcessing.Images;

public class FanArtImageManager(
    ImageRepository imageRepository
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

    public async Task<ICollection<Image>> StoreArtistImages(FanArtArtistDetails fanArtArtistDetails, Guid artistId,
        Artist dbArtist)
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
                });

            List<Image> images = thumbs
                .Concat(logos)
                .Concat(banners)
                .Concat(hdLogos)
                .Concat(artistBackgrounds)
                .ToList();

            return await imageRepository.StoreArtistImages(images, dbArtist);
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return [];
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }

        return [];
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

            foreach (AlbumReleaseGroup albumRelease in dbRelease.AlbumReleaseGroup)
            {
                albumRelease.Album.Cover = albumCover?.FilePath ?? albumRelease.Album.Cover;
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

    public async Task<List<Image>> StoreReleaseImages(Dictionary<Guid, Albums> fanArtArtistAlbums, Guid artistId, Artist dbArtist)
    {
        List<Album> albums = dbArtist.AlbumArtist
            .Select(a => a.Album)
            .ToList();

        Dictionary<Guid, Albums> filteredAlbums = fanArtArtistAlbums
            .Where(fa => albums.Any(a => a.Id == fa.Key))
            .ToDictionary();
        
        List<Image> images = [];
        foreach ((Guid id, Albums fanArtArtistAlbum) in filteredAlbums)
        {
            images.AddRange(await StoreReleaseImages(fanArtArtistAlbum, artistId, dbArtist));
        }

        return images;
    }

    private async Task<ICollection<Image>> StoreReleaseImages(Albums fanArtArtistAlbums, Guid albumId, Artist dbArtist)
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
                })
                .ToList();

            List<Image> covers = fanArtArtistAlbums.Cover
                .Select(image => new Image
                {
                    AspectRatio = 1,
                    Type = "cover",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    AlbumId = albumId,
                    Site = image.Url.BasePath(),
                })
                .ToList();
            
            List<Image> images = cdArts
                .Concat(covers)
                .ToList();

            return await imageRepository.StoreArtistImages(images, dbArtist);
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return [];
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }
        
        return [];
    }

    public static async Task<CoverArtImageManagerManager.CoverPalette?> Add(Guid id, bool priority = false)
    {
        try
        {
            using FanArtMusicClient fanArtMusicClient = new();
            FanArtArtistDetails? fanArt = await fanArtMusicClient.Artist(id);
            if (fanArt is null) return null;

            List<Uri> coverList = fanArt.Thumbs.Select(t => t.Url)
                .ToList();

            foreach (Uri coverItem in coverList)
            {
                if (!coverItem.HasSuccessStatus("image/*")) continue;

                return new()
                {
                    Palette = await ColorPalette("cover", coverItem),
                    Url = coverItem
                };
            }

            return null;
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return null;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
            return null;
        }
    }
}