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

using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.CoverArt.Models;
using Serilog.Events;
using SixLabors.ImageSharp;

namespace NoMercy.MediaProcessing.Images;

public class CoverArtImageManagerManager : ICoverArtImageManagerManager
{
    private static readonly Size PaletteDecodeSize = new(
        width: ColorQuantizer.MaxDimension,
        height: ColorQuantizer.MaxDimension
    );

    public static async Task<string> ColorPalette(string type, Uri url, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(
            client: CoverArtCoverArtClient.Download,
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
            client: CoverArtCoverArtClient.Download,
            items: items,
            download: download,
            maxDecodeSize: PaletteDecodeSize
        );
    }

    public class CoverPalette
    {
        public string? Palette { get; set; }
        public Uri? Url { get; set; }
    }

    public static async Task<Uri?> GetCoverUrl(Guid id, bool priority = false)
    {
        try
        {
            CoverArtCoverArtClient coverArtCoverArtClient = new(id: id);
            CoverArtCovers? covers = await coverArtCoverArtClient.Cover(priority: priority);
            if (covers is null)
                return null;

            CoverArtImage? coverItem = covers.Images.FirstOrDefault(predicate: image =>
                image.Types.Contains(value: "Front")
            );

            return coverItem?.CoverArtThumbnails.Large;
        }
        catch (Exception e)
        {
            if (e.Message.Contains(value: "404"))
                return null;
            Logger.FanArt(message: e.Message, level: LogEventLevel.Verbose);
            return null;
        }
    }

    public static async Task<CoverPalette?> Add(Guid id, bool priority = false)
    {
        try
        {
            CoverArtCoverArtClient coverArtCoverArtClient = new(id: id);
            CoverArtCovers? covers = await coverArtCoverArtClient.GroupCover(priority: priority);
            if (covers is null)
                return null;

            List<CoverArtImage> coverList = covers
                .Images.Where(predicate: image => image.Types.Contains(value: "Front"))
                .ToList();

            foreach (CoverArtImage coverItem in coverList)
            {
                if (!coverItem.CoverArtThumbnails.Large.HasSuccessStatus(contentType: "image/*"))
                    continue;

                return new()
                {
                    Palette = await ColorPalette(type: "cover", url: coverItem.CoverArtThumbnails.Large),
                    Url = coverItem.CoverArtThumbnails.Large,
                };
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
