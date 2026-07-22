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
using NoMercy.NmSystem.Information;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Configuration = AcoustID.Configuration;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.CoverArt.Client;

public class CoverArtCoverArtClient : CoverArtBaseClient
{
    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            message: "CoverArtCoverArtClient has not been initialized. Call CoverArtCoverArtClient.Initialize() at startup."
        );

    public CoverArtCoverArtClient(Guid id)
        : base(id: id)
    {
        Configuration.ClientKey = ApiKeyStore.Current.AcousticIdKey;
    }

    public Task<CoverArtCovers?> Cover(bool priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            //
        };

        try
        {
            return Get<CoverArtCovers>(url: "release/" + Id, query: queryParams, priority: priority);
        }
        catch (Exception)
        {
            return Task.FromResult<CoverArtCovers?>(result: null);
        }
    }

    public Task<CoverArtCovers?> GroupCover(bool priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            //
        };

        try
        {
            return Get<CoverArtCovers>(url: "release-group/" + Id, query: queryParams, priority: priority);
        }
        catch (Exception)
        {
            return Task.FromResult<CoverArtCovers?>(result: null);
        }
    }

    public static async Task<Image<Rgba32>?> Download(
        Uri? url,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        string filePath = Path.Combine(
            path1: AppFiles.MusicImagesPath,
            path2: Path.GetFileName(path: (url?.LocalPath).OrEmpty())
        );

        IStorage storage = Storage;
        if (await storage.ExistsAsync(path: filePath, ct: CancellationToken.None))
        {
            if (maxDecodeSize.HasValue)
            {
                DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                return Image.Load<Rgba32>(options: options, path: filePath);
            }

            return Image.Load<Rgba32>(path: filePath);
        }

        HttpClient httpClient = HttpClientProvider.CreateClient(name: HttpClientNames.CoverArtImage);

        using HttpResponseMessage response = await httpClient.GetAsync(requestUri: url);
        if (!response.IsSuccessStatusCode)
            return null;

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        if (download is not false && !await storage.ExistsAsync(path: filePath, ct: CancellationToken.None))
            await storage.WriteAsync(path: filePath, bytes: bytes, ct: CancellationToken.None);

        if (maxDecodeSize.HasValue)
        {
            DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
            return Image.Load<Rgba32>(options: options, data: bytes);
        }

        return Image.Load<Rgba32>(data: bytes);
    }
}
