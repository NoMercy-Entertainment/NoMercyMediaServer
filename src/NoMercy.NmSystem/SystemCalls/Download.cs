using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;
using Serilog.Events;

namespace NoMercy.NmSystem.SystemCalls;

public static class Download
{
    private static readonly HttpClient HttpClient = new();
    
    static Download()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        
    }

    public static async Task<string> DownloadFile(string name, Uri url, string? outputPath = null)
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
        if (directory is not null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using HttpResponseMessage result = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        result.EnsureSuccessStatusCode();

        long? expectedLength = result.Content.Headers.ContentLength;

        await using (Stream contentStream = await result.Content.ReadAsStreamAsync())
        await using (FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            await contentStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }

        // Verify the file was written successfully
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
                LogEventLevel.Warning);
        }

        Logger.System($"Downloaded {name} to {filePath} ({actualLength} bytes)", LogEventLevel.Verbose);

        return filePath;
    }

    public static Task DeleteSourceDownload(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return Task.CompletedTask;

            if (Locking.IsFileLocked(filePath)) Locking.CloseApplicationLockingFile(filePath);

            File.Delete(filePath);

            Logger.System($"Deleted source download {filePath}", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.System($"Failed to delete source download {filePath}: {ex.Message}", LogEventLevel.Warning);
        }

        return Task.CompletedTask;
    }
}