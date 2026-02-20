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

    public static async Task<string> DownloadFile(string name, Uri url, string? outputName = null)
    {
        Logger.System($"Downloading {name}", LogEventLevel.Verbose);

        string baseName = outputName ?? Path.GetFileName(url.ToString());
        string filePath = Path.Combine(AppFiles.DependenciesPath, baseName);

        using HttpResponseMessage result = await HttpClient.GetAsync(url);
        byte[] content = await result.Content.ReadAsByteArrayAsync();

        await File.WriteAllBytesAsync(filePath, content);
        
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