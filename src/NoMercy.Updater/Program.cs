using System.Diagnostics;
using Newtonsoft.Json;

namespace NoMercy.Updater;

public class NoMercyUpdater
{
    private const string RepoOwner = "NoMercy-Entertainment";
    private const string RepoName = "NoMercyMediaServer";
    private static readonly HttpClient HttpClient = new();
    private const string InstalledVersionFile = "version.txt";

    static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--check")
        {
            bool isUpdateAvailable = await CheckForUpdate();
            Console.WriteLine(JsonConvert.SerializeObject(new
            {
                UpdateAvailable = isUpdateAvailable
            }));
            return;
        }
        
        if (await CheckForUpdate())
        {
            Console.WriteLine("An update is available. Do you want to install it? (y/n)");
            if (Console.ReadKey().Key == ConsoleKey.Y)
            {
                Console.WriteLine("\nDownloading and installing update...");
                await InstallUpdate();
            }
        }
        else
        {
            Console.WriteLine("No updates available.");
        }
    }

    public static async Task<bool> CheckForUpdate()
    {
        string? osIdentifier = GetOsIdentifier();
        if (string.IsNullOrEmpty(osIdentifier)) return false;

        string installedCommit = GetInstalledCommitHash();
        (string latestCommit, _) = await GetLatestReleaseInfo(osIdentifier);

        return installedCommit != latestCommit;
    }

    public static async Task InstallUpdate()
    {
        string? osIdentifier = GetOsIdentifier();
        if (string.IsNullOrEmpty(osIdentifier)) return;

        (_, string? downloadUrl) = await GetLatestReleaseInfo(osIdentifier);
        if (string.IsNullOrEmpty(downloadUrl)) return;

        string fileName = Path.Combine(Directory.GetCurrentDirectory(), "update_temp" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        
        byte[] data = await HttpClient.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(fileName, data);
        
        Console.WriteLine("Stopping NoMercyMediaServer...");
        StopMediaServer();
        
        string binaryPath = Path.Combine(Directory.GetCurrentDirectory(), "NoMercyMediaServer" + (OperatingSystem.IsWindows() ? ".exe" : ""));

        Console.WriteLine("Installing update...");
        File.Move(fileName, binaryPath, true);
        
        MakeExecutable(binaryPath);

        Console.WriteLine("Restarting NoMercyMediaServer...");
        StartMediaServer();
    }

    private static string? GetOsIdentifier()
    {
        if (OperatingSystem.IsWindows()) return "windows-x64";
        if (OperatingSystem.IsLinux()) return "linux-x64";
        if (OperatingSystem.IsMacOS()) return "macos-x64";
        return null;
    }

    private static string GetInstalledCommitHash()
    {
        return File.Exists(InstalledVersionFile) 
            ? File.ReadAllText(InstalledVersionFile).Trim() 
            : "unknown";
    }

    private class Release
    {
        [JsonProperty("assets")] public List<Asset> Assets { get; set; } = [];
    }
    
    private class Asset
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("browser_download_url")] public string DownloadUrl { get; set; } = "";
    }

    private static async Task<(string commitHash, string? downloadUrl)> GetLatestReleaseInfo(string? osIdentifier)
    {
        string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        string response = await HttpClient.GetStringAsync(url);
        Release? doc = JsonConvert.DeserializeObject<Release>(response);

        foreach (Asset asset in doc?.Assets ?? [])
        {
            string fileName = asset.Name;
            if (osIdentifier == null || !fileName.Contains(osIdentifier)) continue;
            
            string commitHash = fileName.Split('-')[^1].Replace(".exe", "").Trim();
            string downloadUrl = asset.DownloadUrl;
            return (commitHash, downloadUrl);
        }
        return ("unknown", null);
    }

    private static void MakeExecutable(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Process.Start("chmod", $"+x {filePath}").WaitForExit();
        }
    }

    private static void StopMediaServer()
    {
        foreach (Process process in Process.GetProcesses())
        {
            if (!process.ProcessName.StartsWith("NoMercyMediaServer")) continue;
            try
            {
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to stop server: {ex.Message}");
            }
        }
    }

    private static void StartMediaServer()
    {
        try
        {
            Process.Start("./NoMercyMediaServer");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restart server: {ex.Message}");
        }
    }
}