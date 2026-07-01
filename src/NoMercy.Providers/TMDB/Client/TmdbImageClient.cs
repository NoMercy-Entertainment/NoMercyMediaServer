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
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.TMDB.Client;

public abstract class TmdbImageClient
{
    public const string ImageBaseUrl = "https://image.tmdb.org/t/p/";

    // Image downloads hit image.tmdb.org (a separate host from the API) and
    // are throttled by their own queue rather than the shared API queue.
    private static readonly Queue ImageQueue = new(
        new()
        {
            Concurrent = 50,
            Interval = 1000,
            Start = true,
        }
    );

    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "TmdbImageClient has not been initialized. Call TmdbImageClient.Initialize() at startup."
        );

    public static Task<Image<Rgba32>?>? Download(
        string? path,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        try
        {
            return ImageQueue.Enqueue(Task, path, true);
        }
        catch (InvalidImageContentException e)
        {
            Logger.MovieDb(
                $"Image format error downloading image: {path} - {e.Message}",
                LogEventLevel.Error
            );
            return null;
        }
        catch (ImageFormatException e)
        {
            Logger.MovieDb(
                $"Image format error downloading image: {path} - {e.Message}",
                LogEventLevel.Error
            );
            return null;
        }

        async Task<Image<Rgba32>?> Task()
        {
            try
            {
                if (path is null)
                    return null;

                // A null/empty image path (an entity with no poster/backdrop) must
                // never reach the write below: path.Replace("/", "") collapses to
                // an empty file name, so filePath becomes the 'original' folder
                // itself and WriteAsync targets the directory — "Access to the path
                // '…/cache/images/original' is denied". Nothing to download here.
                string fileName = path.Replace("/", "");
                if (string.IsNullOrWhiteSpace(fileName))
                    return null;

                bool isSvg = path.EndsWith(".svg");
                string folder = Path.Join(AppFiles.ImagesPath, "original");

                IStorage storage = Storage;
                await storage.CreateDirectoryAsync(folder, CancellationToken.None);

                string filePath = Path.Join(folder, fileName);
                if (await storage.ExistsAsync(filePath, CancellationToken.None))
                {
                    if (isSvg)
                        return null;

                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return await Image.LoadAsync<Rgba32>(options, filePath);
                    }

                    return await Image.LoadAsync<Rgba32>(filePath);
                }

                HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.TmdbImage);

                string url = path.StartsWith("http") ? path : $"original{path}";
                using HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (download is false)
                {
                    if (isSvg)
                        return null;

                    await using Stream contentStream = await response.Content.ReadAsStreamAsync();

                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return Image.Load<Rgba32>(options, contentStream);
                    }

                    return Image.Load<Rgba32>(contentStream);
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                if (!await storage.ExistsAsync(filePath, CancellationToken.None))
                    await storage.WriteAsync(filePath, bytes, CancellationToken.None);

                try
                {
                    if (isSvg)
                        return null;

                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return Image.Load<Rgba32>(options, filePath);
                    }

                    return Image.Load<Rgba32>(filePath);
                }
                catch (Exception e)
                {
                    Logger.MovieDb(
                        $"Error loading image: {path} - {e.Message}",
                        LogEventLevel.Error
                    );
                    return null;
                }
            }
            catch (InvalidImageContentException e)
            {
                Logger.MovieDb(
                    $"Image format error downloading image: {path} - {e.Message}",
                    LogEventLevel.Error
                );
                return null;
            }
            catch (ImageFormatException e)
            {
                Logger.MovieDb(
                    $"Image format error downloading image: {path} - {e.Message}",
                    LogEventLevel.Error
                );
                return null;
            }
        }
    }
}
