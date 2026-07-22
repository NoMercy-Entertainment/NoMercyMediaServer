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

namespace NoMercy.Providers.NoMercy.Client;

public abstract class NoMercyImageClient
{
    // Image downloads use their own queue rather than the shared TMDB API queue.
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
            message: "NoMercyImageClient has not been initialized. Call NoMercyImageClient.Initialize() at startup."
        );

    public static Task<Image<Rgba32>?> Download(
        string? path,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        return ImageQueue.Enqueue(task: Task, url: $"original{path}", priority: true);

        async Task<Image<Rgba32>?> Task()
        {
            if (path is null)
                return null;

            try
            {
                string folder = Path.Join(path1: AppFiles.ImagesPath, path2: "original");

                IStorage storage = Storage;
                await storage.CreateDirectoryAsync(path: folder, ct: CancellationToken.None);

                string filePath = Path.Combine(path1: folder, path2: path.Replace(oldValue: "/", newValue: "").Replace(oldValue: "\\", newValue: ""));

                if (await storage.ExistsAsync(path: filePath, ct: CancellationToken.None))
                {
                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return Image.Load<Rgba32>(options: options, path: filePath);
                    }

                    return Image.Load<Rgba32>(path: filePath);
                }

                HttpClient httpClient = HttpClientProvider.CreateClient(
                    name: HttpClientNames.NoMercyImage
                );

                string url = path.StartsWith(value: "http") ? path : $"original{path}";

                using HttpResponseMessage response = await httpClient.GetAsync(requestUri: url);
                if (!response.IsSuccessStatusCode)
                    return null;

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                if (
                    download is not false
                    && !await storage.ExistsAsync(path: filePath, ct: CancellationToken.None)
                )
                    await storage.WriteAsync(path: filePath, bytes: bytes, ct: CancellationToken.None);

                if (maxDecodeSize.HasValue)
                {
                    DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                    return Image.Load<Rgba32>(options: options, data: bytes);
                }

                return Image.Load<Rgba32>(data: bytes);
            }
            catch (Exception e)
            {
                Logger.MovieDb(
                    message: $"Error downloading image: {path} - {e.Message}",
                    level: LogEventLevel.Error
                );
            }

            return null;
        }
    }
}
