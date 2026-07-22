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

using NoMercy.Providers.NoMercy.Client;
using SixLabors.ImageSharp;

namespace NoMercy.MediaProcessing.Images;

public abstract class NoMercyImageManager : INoMercyImageManager
{
    private static readonly Size PaletteDecodeSize = new(
        width: ColorQuantizer.MaxDimension,
        height: ColorQuantizer.MaxDimension
    );

    public static async Task<string> ColorPalette(string type, string? path, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(
            client: NoMercyImageClient.Download,
            type: type,
            path: path,
            download: download,
            maxDecodeSize: PaletteDecodeSize
        );
    }

    public static async Task<string> MultiColorPalette(
        IEnumerable<BaseImageManager.MultiStringType> items,
        bool? download = true
    )
    {
        return await BaseImageManager.MultiColorPalette(
            client: NoMercyImageClient.Download,
            items: items,
            download: download,
            maxDecodeSize: PaletteDecodeSize
        );
    }
}
