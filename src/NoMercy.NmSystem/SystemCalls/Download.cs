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

        using HttpResponseMessage result = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        result.EnsureSuccessStatusCode();

        await using Stream contentStream = await result.Content.ReadAsStreamAsync();
        await using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await contentStream.CopyToAsync(fileStream);

        Logger.System($"Downloaded {name} to {filePath}", LogEventLevel.Verbose);

        return filePath;
    }

    public static async Task DeleteSourceDownload(string filePath)
    {
        try
        {
            if(!File.Exists(filePath)) return;
            
            if (Locking.IsFileLocked(filePath)) Locking.CloseApplicationLockingFile(filePath);

            File.Delete(filePath);
            
            Logger.System($"Deleted source download {filePath}", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.System($"Failed to delete source download {filePath}: {ex.Message}", LogEventLevel.Warning);
        }

        await Task.Delay(0);
    }
}