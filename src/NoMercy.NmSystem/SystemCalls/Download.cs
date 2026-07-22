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

using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.NmSystem.SystemCalls;

public static class Download
{
    private static readonly HttpClient HttpClient = new();

    static Download()
    {
        HttpClient.DefaultRequestHeaders.Add(
            name: "User-Agent",
            value: ExternalServicesConfig.Current.UserAgent
        );
    }

    public static async Task<string> DownloadFile(
        IStorage storage,
        string name,
        Uri url,
        string? outputPath = null
    )
    {
        Logger.System(message: $"Downloading {name}", level: LogEventLevel.Verbose);

        string filePath;
        if (outputPath is not null && Path.IsPathRooted(path: outputPath))
        {
            filePath = outputPath;
        }
        else
        {
            string baseName = outputPath ?? Path.GetFileName(path: url.ToString());
            filePath = Path.Combine(path1: AppFiles.DependenciesPath, path2: baseName);
        }

        string? directory = Path.GetDirectoryName(path: filePath);
        if (directory is not null && !storage.Exists(path: directory))
            storage.CreateDirectory(path: directory);

        using HttpResponseMessage result = await HttpClient.GetAsync(
            requestUri: url,
            completionOption: HttpCompletionOption.ResponseHeadersRead
        );
        result.EnsureSuccessStatusCode();

        long? expectedLength = result.Content.Headers.ContentLength;

        await using (Stream contentStream = await result.Content.ReadAsStreamAsync())
        await using (Stream fileStream = storage.OpenWrite(path: filePath, overwrite: true))
        {
            await contentStream.CopyToAsync(destination: fileStream);
            await fileStream.FlushAsync();
        }

        if (!storage.Exists(path: filePath))
            throw new IOException(message: $"Download of {name} completed but file not found at {filePath}");

        long actualLength = storage.SizeOrZero(path: filePath);
        if (actualLength == 0)
        {
            storage.Delete(path: filePath);
            throw new IOException(message: $"Download of {name} produced an empty file at {filePath}");
        }

        if (expectedLength.HasValue && actualLength != expectedLength.Value)
        {
            Logger.System(
                message: $"Download of {name}: size mismatch (expected {expectedLength.Value} bytes, got {actualLength} bytes)",
                level: LogEventLevel.Warning
            );
        }

        Logger.System(
            message: $"Downloaded {name} to {filePath} ({actualLength} bytes)",
            level: LogEventLevel.Verbose
        );

        return filePath;
    }

    public static Task DeleteSourceDownload(IStorage storage, string filePath)
    {
        try
        {
            if (!storage.Exists(path: filePath))
                return Task.CompletedTask;

            if (Locking.IsFileLocked(filePath: filePath))
                Locking.CloseApplicationLockingFile(filePath: filePath);

            storage.Delete(path: filePath);

            Logger.System(message: $"Deleted source download {filePath}", level: LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.System(
                message: $"Failed to delete source download {filePath}: {ex.Message}",
                level: LogEventLevel.Warning
            );
        }

        return Task.CompletedTask;
    }
}
