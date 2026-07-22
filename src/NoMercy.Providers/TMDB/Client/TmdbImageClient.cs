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
        options: new()
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
            message: "TmdbImageClient has not been initialized. Call TmdbImageClient.Initialize() at startup."
        );

    public static Task<Image<Rgba32>?>? Download(
        string? path,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        try
        {
            return ImageQueue.Enqueue(task: Task, url: path, priority: true);
        }
        catch (InvalidImageContentException e)
        {
            Logger.MovieDb(
                message: $"Image format error downloading image: {path} - {e.Message}",
                level: LogEventLevel.Error
            );
            return null;
        }
        catch (ImageFormatException e)
        {
            Logger.MovieDb(
                message: $"Image format error downloading image: {path} - {e.Message}",
                level: LogEventLevel.Error
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
                string fileName = path.Replace(oldValue: "/", newValue: "");
                if (string.IsNullOrWhiteSpace(value: fileName))
                    return null;

                bool isSvg = path.EndsWith(value: ".svg");
                string folder = Path.Join(path1: AppFiles.ImagesPath, path2: "original");

                IStorage storage = Storage;
                await storage.CreateDirectoryAsync(path: folder, ct: CancellationToken.None);

                string filePath = Path.Join(path1: folder, path2: fileName);
                if (await storage.ExistsAsync(path: filePath, ct: CancellationToken.None))
                {
                    if (isSvg)
                        return null;

                    try
                    {
                        if (maxDecodeSize.HasValue)
                        {
                            DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                            return await Image.LoadAsync<Rgba32>(options: options, path: filePath);
                        }

                        return await Image.LoadAsync<Rgba32>(path: filePath);
                    }
                    catch (Exception e)
                        when (e is ImageFormatException or InvalidImageContentException)
                    {
                        // A poisoned cache entry (a non-image body persisted by an
                        // older build, or a truncated write). Delete it so this run
                        // re-downloads a clean copy instead of failing on every
                        // scan forever.
                        Logger.MovieDb(
                            message: $"Discarding undecodable cached image, re-downloading: {path} - {e.Message}",
                            level: LogEventLevel.Warning
                        );
                        await storage.DeleteAsync(path: filePath, ct: CancellationToken.None);
                    }
                }

                HttpClient httpClient = HttpClientProvider.CreateClient(name: HttpClientNames.TmdbImage);

                string url = path.StartsWith(value: "http") ? path : $"original{path}";
                using HttpResponseMessage response = await httpClient.GetAsync(requestUri: url);

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
                        return Image.Load<Rgba32>(options: options, stream: contentStream);
                    }

                    return Image.Load<Rgba32>(stream: contentStream);
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                if (isSvg)
                {
                    // SVG is not decoded (ImageSharp has no SVG decoder), but it
                    // is a valid asset — cache it as-is.
                    if (!await storage.ExistsAsync(path: filePath, ct: CancellationToken.None))
                        await storage.WriteAsync(path: filePath, bytes: bytes, ct: CancellationToken.None);
                    return null;
                }

                DecoderOptions? decoderOptions = maxDecodeSize.HasValue
                    ? new() { TargetSize = maxDecodeSize.Value }
                    : null;

                try
                {
                    // Validate the downloaded bytes decode as an image BEFORE
                    // persisting. A 200 response can still carry a non-image body
                    // (an HTML error page, a truncated CDN response); writing it
                    // first poisons the cache — the bad file then satisfies the
                    // Exists check on every later run and the decode fails
                    // forever. This probe image is disposed immediately; only a
                    // validated download reaches disk, so a transient bad
                    // download can be re-fetched cleanly next time.
                    if (decoderOptions is not null)
                    {
                        using Image<Rgba32> probe = Image.Load<Rgba32>(options: decoderOptions, data: bytes);
                    }
                    else
                    {
                        using Image<Rgba32> probe = Image.Load<Rgba32>(data: bytes);
                    }

                    if (!await storage.ExistsAsync(path: filePath, ct: CancellationToken.None))
                        await storage.WriteAsync(path: filePath, bytes: bytes, ct: CancellationToken.None);
                }
                catch (Exception e)
                {
                    Logger.MovieDb(
                        message: $"Error loading image: {path} - {e.Message}",
                        level: LogEventLevel.Error
                    );
                    return null;
                }

                // Ownership transfers to the caller (who disposes it); load from
                // the now-validated bytes.
                if (decoderOptions is not null)
                    return Image.Load<Rgba32>(options: decoderOptions, data: bytes);

                return Image.Load<Rgba32>(data: bytes);
            }
            catch (InvalidImageContentException e)
            {
                Logger.MovieDb(
                    message: $"Image format error downloading image: {path} - {e.Message}",
                    level: LogEventLevel.Error
                );
                return null;
            }
            catch (ImageFormatException e)
            {
                Logger.MovieDb(
                    message: $"Image format error downloading image: {path} - {e.Message}",
                    level: LogEventLevel.Error
                );
                return null;
            }
        }
    }
}
