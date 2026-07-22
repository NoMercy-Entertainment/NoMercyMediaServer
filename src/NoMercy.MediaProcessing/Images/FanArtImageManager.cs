// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using Serilog.Events;
using SixLabors.ImageSharp;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.MediaProcessing.Images;

public class FanArtImageManager(ImageRepository imageRepository) : IFanArtImageManager
{
    private static readonly Size PaletteDecodeSize = new(
        width: ColorQuantizer.MaxDimension,
        height: ColorQuantizer.MaxDimension
    );

    public static async Task<string> ColorPalette(string type, Uri url, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(
            client: FanArtImageClient.Download,
            type: type,
            path: url,
            download: download,
            maxDecodeSize: PaletteDecodeSize
        );
    }

    public async Task<string> MultiColorPalette(
        IEnumerable<BaseImageManager.MultiUriType> items,
        bool? download = true
    )
    {
        return await BaseImageManager.MultiColorPalette(
            client: FanArtImageClient.Download,
            items: items,
            download: download,
            maxDecodeSize: PaletteDecodeSize
        );
    }

    public async Task<ICollection<Image>> StoreArtistImages(
        FanArtArtistDetails fanArtArtistDetails,
        Guid artistId,
        Artist dbArtist
    )
    {
        try
        {
            List<Image> thumbs = fanArtArtistDetails
                .Thumbs.ToList()
                .ConvertAll<Image>(converter: image =>
                    new()
                    {
                        AspectRatio = 1,
                        Type = "thumb",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        ArtistId = artistId,
                        Site = image.Url.BasePath(),
                    }
                );
            List<Image> logos = fanArtArtistDetails
                .Logos.ToList()
                .ConvertAll<Image>(converter: image =>
                    new()
                    {
                        AspectRatio = 1,
                        Type = "logo",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        ArtistId = artistId,
                        Site = image.Url.BasePath(),
                    }
                );
            List<Image> banners = fanArtArtistDetails
                .Banners.ToList()
                .ConvertAll<Image>(converter: image =>
                    new()
                    {
                        AspectRatio = 1,
                        Type = "banner",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        ArtistId = artistId,
                        Site = image.Url.BasePath(),
                    }
                );
            List<Image> hdLogos = fanArtArtistDetails
                .HdLogos.ToList()
                .ConvertAll<Image>(converter: image =>
                    new()
                    {
                        AspectRatio = 1,
                        Type = "hdLogo",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        ArtistId = artistId,
                        Site = image.Url.BasePath(),
                    }
                );
            List<Image> artistBackgrounds = fanArtArtistDetails
                .Backgrounds.ToList()
                .ConvertAll<Image>(converter: image =>
                    new()
                    {
                        AspectRatio = 1,
                        Type = "background",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        ArtistId = artistId,
                        Site = image.Url.BasePath(),
                    }
                );

            List<Image> images = thumbs
                .Concat(second: logos)
                .Concat(second: banners)
                .Concat(second: hdLogos)
                .Concat(second: artistBackgrounds)
                .ToList();

            return await imageRepository.StoreArtistImages(images: images, dbArtist: dbArtist);
        }
        catch (Exception e)
        {
            if (e.Message.Contains(value: "404"))
                return [];
            Logger.FanArt(message: e.Message, level: LogEventLevel.Verbose);
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
                covers.AddRange(
                    collection: albums.Cover.Select(selector: image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cover",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        AlbumId = releaseId,
                        Site = image.Url.BasePath(),
                        Name = fanArt.Name,
                    })
                );

                cdArts.AddRange(
                    collection: albums.CdArt.Select(selector: image => new Image
                    {
                        AspectRatio = 1,
                        Type = "cdArt",
                        VoteCount = image.Likes,
                        FilePath = "/" + image.Url.FileName(),
                        AlbumId = releaseId,
                        Site = image.Url.BasePath(),
                        Name = fanArt.Name,
                    })
                );
            }

            ReleaseGroup dbRelease = await imageRepository.GetReleaseImages(id: releaseId);

            IEnumerable<Image> images = covers
                .Concat(second: cdArts)
                .Where(predicate: image => dbRelease.AlbumReleaseGroup.Any(predicate: ar => ar.AlbumId == image.AlbumId));

            Image? albumCover = covers.FirstOrDefault();

            dbRelease.Cover = albumCover?.FilePath ?? dbRelease.Cover;

            foreach (AlbumReleaseGroup albumRelease in dbRelease.AlbumReleaseGroup)
            {
                albumRelease.Album.Cover = albumCover?.FilePath ?? albumRelease.Album.Cover;
            }

            await imageRepository.CommitReleaseChanges();
            await imageRepository.StoreReleaseImages(images: images);
        }
        catch (Exception e)
        {
            if (e.Message.Contains(value: "404"))
                return;
            Logger.FanArt(message: e.Message, level: LogEventLevel.Verbose);
        }
    }

    public async Task<List<Image>> StoreReleaseImages(
        Dictionary<Guid, Albums> fanArtArtistAlbums,
        Guid artistId,
        Artist dbArtist
    )
    {
        List<Album> albums = dbArtist.AlbumArtist.Select(selector: a => a.Album).ToList();

        Dictionary<Guid, Albums> filteredAlbums = fanArtArtistAlbums
            .Where(predicate: fa => albums.Any(predicate: a => a.Id == fa.Key))
            .ToDictionary();

        List<Image> images = [];
        foreach ((Guid id, Albums fanArtArtistAlbum) in filteredAlbums)
        {
            images.AddRange(collection: await StoreReleaseImages(fanArtArtistAlbums: fanArtArtistAlbum, albumId: artistId, dbArtist: dbArtist));
        }

        return images;
    }

    private async Task<ICollection<Image>> StoreReleaseImages(
        Albums fanArtArtistAlbums,
        Guid albumId,
        Artist dbArtist
    )
    {
        try
        {
            List<Image> cdArts = fanArtArtistAlbums
                .CdArt.Select(selector: image => new Image
                {
                    AspectRatio = 1,
                    Type = "cdArt",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    AlbumId = albumId,
                    Site = image.Url.BasePath(),
                })
                .ToList();

            List<Image> covers = fanArtArtistAlbums
                .Cover.Select(selector: image => new Image
                {
                    AspectRatio = 1,
                    Type = "cover",
                    VoteCount = image.Likes,
                    FilePath = "/" + image.Url.FileName(),
                    AlbumId = albumId,
                    Site = image.Url.BasePath(),
                })
                .ToList();

            List<Image> images = cdArts.Concat(second: covers).ToList();

            return await imageRepository.StoreArtistImages(images: images, dbArtist: dbArtist);
        }
        catch (Exception e)
        {
            if (e.Message.Contains(value: "404"))
                return [];
            Logger.FanArt(message: e.Message, level: LogEventLevel.Verbose);
        }

        return [];
    }

    public static async Task<CoverArtImageManagerManager.CoverPalette?> Add(
        Guid id,
        bool priority = false
    )
    {
        try
        {
            using FanArtMusicClient fanArtMusicClient = new();
            FanArtArtistDetails? fanArt = await fanArtMusicClient.Artist(id: id);
            if (fanArt is null)
                return null;

            List<Uri> coverList = fanArt.Thumbs.Select(selector: t => t.Url).ToList();

            foreach (Uri coverItem in coverList)
            {
                if (!coverItem.HasSuccessStatus(contentType: "image/*"))
                    continue;

                return new() { Palette = await ColorPalette(type: "cover", url: coverItem), Url = coverItem };
            }

            return null;
        }
        catch (Exception e)
        {
            if (e.Message.Contains(value: "404"))
                return null;
            Logger.FanArt(message: e.Message, level: LogEventLevel.Verbose);
            return null;
        }
    }
}
