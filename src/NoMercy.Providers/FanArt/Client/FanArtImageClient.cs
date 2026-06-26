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

using NoMercy.NmSystem.Information;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Configuration = AcoustID.Configuration;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtImageClient : FanArtBaseClient
{
    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "FanArtImageClient has not been initialized. Call FanArtImageClient.Initialize() at startup."
        );

    public FanArtImageClient()
    {
        Configuration.ClientKey = ApiKeyStore.Current.AcousticIdKey;
    }

    public FanArtImageClient(Guid id)
        : base(id)
    {
        Configuration.ClientKey = ApiKeyStore.Current.AcousticIdKey;
    }

    public Task<CoverArtCovers?> Cover(bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<CoverArtCovers>("release/" + Id, queryParams, priority);
    }

    public static async Task<Image<Rgba32>?> Download(
        Uri url,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        string filePath = Path.Combine(AppFiles.MusicImagesPath, Path.GetFileName(url.LocalPath));

        IStorage storage = Storage;
        if (await storage.ExistsAsync(filePath, CancellationToken.None))
        {
            if (maxDecodeSize.HasValue)
            {
                DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                return Image.Load<Rgba32>(options, filePath);
            }

            return Image.Load<Rgba32>(filePath);
        }

        HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.FanArtImage);

        using HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        if (download is not false && !await storage.ExistsAsync(filePath, CancellationToken.None))
            await storage.WriteAsync(filePath, bytes, CancellationToken.None);

        if (maxDecodeSize.HasValue)
        {
            DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
            return Image.Load<Rgba32>(options, bytes);
        }

        return Image.Load<Rgba32>(bytes);
    }
}
