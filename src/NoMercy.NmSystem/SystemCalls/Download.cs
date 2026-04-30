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
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
    }

    public static async Task<string> DownloadFile(
        IStorage storage,
        string name,
        Uri url,
        string? outputPath = null
    )
    {
        Logger.System($"Downloading {name}", LogEventLevel.Verbose);

        string filePath;
        if (outputPath is not null && Path.IsPathRooted(outputPath))
        {
            filePath = outputPath;
        }
        else
        {
            string baseName = outputPath ?? Path.GetFileName(url.ToString());
            filePath = Path.Combine(AppFiles.DependenciesPath, baseName);
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (directory is not null && !storage.Exists(directory))
            storage.CreateDirectory(directory);

        using HttpResponseMessage result = await HttpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead
        );
        result.EnsureSuccessStatusCode();

        long? expectedLength = result.Content.Headers.ContentLength;

        await using (Stream contentStream = await result.Content.ReadAsStreamAsync())
        await using (Stream fileStream = storage.OpenWrite(filePath, overwrite: true))
        {
            await contentStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }

        if (!storage.Exists(filePath))
            throw new IOException($"Download of {name} completed but file not found at {filePath}");

        long actualLength = storage.SizeOrZero(filePath);
        if (actualLength == 0)
        {
            storage.Delete(filePath);
            throw new IOException($"Download of {name} produced an empty file at {filePath}");
        }

        if (expectedLength.HasValue && actualLength != expectedLength.Value)
        {
            Logger.System(
                $"Download of {name}: size mismatch (expected {expectedLength.Value} bytes, got {actualLength} bytes)",
                LogEventLevel.Warning
            );
        }

        Logger.System(
            $"Downloaded {name} to {filePath} ({actualLength} bytes)",
            LogEventLevel.Verbose
        );

        return filePath;
    }

    // Backward-compatible overload — callers in NoMercy.Setup will be migrated in the Tier-3 pass.
    public static Task<string> DownloadFile(string name, Uri url, string? outputPath = null)
    {
        string filePath;
        if (outputPath is not null && Path.IsPathRooted(outputPath))
        {
            filePath = outputPath;
        }
        else
        {
            string baseName = outputPath ?? Path.GetFileName(url.ToString());
            filePath = Path.Combine(AppFiles.DependenciesPath, baseName);
        }

        string? dir = Path.GetDirectoryName(filePath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return DownloadFileLegacy(name, url, filePath);
    }

    private static async Task<string> DownloadFileLegacy(string name, Uri url, string filePath)
    {
        Logger.System($"Downloading {name}", LogEventLevel.Verbose);

        using HttpResponseMessage result = await HttpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead
        );
        result.EnsureSuccessStatusCode();

        long? expectedLength = result.Content.Headers.ContentLength;

        await using (Stream contentStream = await result.Content.ReadAsStreamAsync())
        await using (
            FileStream fileStream = new(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                true
            )
        )
        {
            await contentStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }

        if (!File.Exists(filePath))
            throw new IOException($"Download of {name} completed but file not found at {filePath}");

        long actualLength = new FileInfo(filePath).Length;
        if (actualLength == 0)
        {
            File.Delete(filePath);
            throw new IOException($"Download of {name} produced an empty file at {filePath}");
        }

        if (expectedLength.HasValue && actualLength != expectedLength.Value)
        {
            Logger.System(
                $"Download of {name}: size mismatch (expected {expectedLength.Value} bytes, got {actualLength} bytes)",
                LogEventLevel.Warning
            );
        }

        Logger.System(
            $"Downloaded {name} to {filePath} ({actualLength} bytes)",
            LogEventLevel.Verbose
        );

        return filePath;
    }

    public static Task DeleteSourceDownload(IStorage storage, string filePath)
    {
        try
        {
            if (!storage.Exists(filePath))
                return Task.CompletedTask;

            if (Locking.IsFileLocked(filePath))
                Locking.CloseApplicationLockingFile(filePath);

            storage.Delete(filePath);

            Logger.System($"Deleted source download {filePath}", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to delete source download {filePath}: {ex.Message}",
                LogEventLevel.Warning
            );
        }

        return Task.CompletedTask;
    }

    // Backward-compatible overload — callers in NoMercy.Setup will be migrated in the Tier-3 pass.
    public static Task DeleteSourceDownload(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Task.CompletedTask;

            if (Locking.IsFileLocked(filePath))
                Locking.CloseApplicationLockingFile(filePath);

            File.Delete(filePath);

            Logger.System($"Deleted source download {filePath}", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to delete source download {filePath}: {ex.Message}",
                LogEventLevel.Warning
            );
        }

        return Task.CompletedTask;
    }
}
