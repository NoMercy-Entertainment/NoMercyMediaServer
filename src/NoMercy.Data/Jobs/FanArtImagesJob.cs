using Microsoft.EntityFrameworkCore;
using NoMercy.MediaProcessing.Images;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;
using Serilog.Events;
using Image = NoMercy.Database.Models.Image;

namespace NoMercy.Data.Jobs;

[Serializable]
public class FanArtImagesJob : IShouldQueue
{
    public MusicBrainzArtist? MusicBrainzArtist { get; set; }
    public MusicBrainzReleaseAppends? MusicBrainzRelease { get; set; }

    public FanArtImagesJob()
    {
        //
    }

    public FanArtImagesJob(MusicBrainzArtist musicBrainzArtist)
    {
        MusicBrainzArtist = musicBrainzArtist;
    }

    public FanArtImagesJob(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        MusicBrainzRelease = musicBrainzRelease;
    }

    public async Task Handle()
    {
        if (MusicBrainzArtist is not null)
            await StoreArtist(MusicBrainzArtist);

        if (MusicBrainzRelease is not null)
            await StoreRelease(MusicBrainzRelease);
    }

    public async Task StoreArtist(MusicBrainzArtist musicBrainzArtist)
    {
        try
        {
            using FanArtMusicClient fanArtMusicClient = new();
            FanArtArtistDetails? fanArt = await fanArtMusicClient.Artist(musicBrainzArtist.Id);
            if (fanArt is null) return;

            List<Image> thumbs = fanArt.Thumbs.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "thumb",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = musicBrainzArtist.Id,
                    Site = image.Url.BasePath(),
                    _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                });

            List<Image> logos = fanArt.Logos.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "logo",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = musicBrainzArtist.Id,
                    Site = image.Url.BasePath(),
                    _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                });
            List<Image> banners = fanArt.Banners.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "banner",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = musicBrainzArtist.Id,
                    Site = image.Url.BasePath(),
                    _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                });
            List<Image> hdLogos = fanArt.HdLogos.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "hdLogo",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = musicBrainzArtist.Id,
                    Site = image.Url.BasePath(),
                    _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                });
            List<Image> artistBackgrounds = fanArt.Backgrounds.ToList()
                .ConvertAll<Image>(image => new()
                {
                    AspectRatio = 1,
                    Type = "background",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    ArtistId = musicBrainzArtist.Id,
                    Site = image.Url.BasePath(),
                    _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                });

            List<Image> images = thumbs
                .Concat(logos)
                .Concat(banners)
                .Concat(hdLogos)
                .Concat(artistBackgrounds)
                .ToList();

            await using MediaContext mediaContext = new();
            Artist dbArtist = await mediaContext.Artists
                .FirstAsync(a => a.Id == musicBrainzArtist.Id);

            Image? artistCover = thumbs.FirstOrDefault();
            dbArtist.Cover = artistCover?.FilePath ?? dbArtist.Cover;

            dbArtist._colorPalette = artistCover?._colorPalette.Replace("\"image\"", "\"cover\"")
                                     ?? dbArtist._colorPalette;

            await mediaContext.SaveChangesAsync();

            await mediaContext.Images.UpsertRange(images)
                .On(v => new { v.FilePath, v.ArtistId })
                .WhenMatched((s, i) => new()
                {
                    Id = i.Id,
                    AspectRatio = i.AspectRatio,
                    Height = i.Height,
                    FilePath = i.FilePath,
                    Width = i.Width,
                    VoteCount = i.VoteCount,
                    ArtistId = i.ArtistId,
                    Type = i.Type,
                    Site = i.Site,
                    _colorPalette = i._colorPalette,
                    UpdatedAt = i.UpdatedAt
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }
    }

    public async Task StoreRelease(MusicBrainzReleaseAppends musicBrainzRelease)
    {
        try
        {
            using FanArtMusicClient fanArtMusicClient = new();
            FanArtAlbum? fanArt = await fanArtMusicClient.Album(musicBrainzRelease.MusicBrainzReleaseGroup.Id);
            if (fanArt is null) return;

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
                        ArtistId = musicBrainzRelease.Id,
                        Site = image.Url.BasePath(),
                        Name = fanArt.Name,
                        _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                    }));

                cdArts.AddRange(albums.CdArt
                    .Select(image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cdArt",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        ArtistId = musicBrainzRelease.Id,
                        Site = image.Url.BasePath(),
                        Name = fanArt.Name,
                        _colorPalette = FanArtImageManager.ColorPalette("image", image.Url).Result
                    }));
            }

            await using MediaContext mediaContext = new();
            ReleaseGroup dbRelease = await mediaContext.ReleaseGroups
                .Include(a => a.AlbumReleaseGroup)
                .ThenInclude(a => a.Album)
                .FirstAsync(a => a.Id == musicBrainzRelease.MusicBrainzReleaseGroup.Id);

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

            await mediaContext.SaveChangesAsync();

            await mediaContext.Images.UpsertRange(images)
                .On(v => new { v.FilePath, v.AlbumId })
                .WhenMatched((s, i) => new()
                {
                    Id = i.Id,
                    AspectRatio = i.AspectRatio,
                    Name = i.Name,
                    Height = i.Height,
                    FilePath = i.FilePath,
                    Width = i.Width,
                    VoteCount = i.VoteCount,
                    AlbumId = i.AlbumId,
                    Type = i.Type,
                    Site = i.Site,
                    _colorPalette = i._colorPalette,
                    UpdatedAt = i.UpdatedAt
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
        }
    }
}