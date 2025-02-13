using System.Diagnostics;
using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.Server.app.Helper;
using Semver;

namespace NoMercy.Updater;

public class NoMercyUpdater
{
    private const string RepoOwner = "NoMercy-Entertainment";
    private const string RepoName = "NoMercyMediaServer";
    private const string Url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    
    private static readonly HttpClient HttpClient = new();
    
    static async Task Main(string[] args)
    {
        Console.Title = "NoMercy Updater";
        await ConsoleMessages.Logo();
        
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
        
        if (args.Length > 0 && args[0] == "--check")
        {
            bool isUpdateAvailable = await CheckForUpdate();
            Logger.Setup(JsonConvert.SerializeObject(new
            {
                UpdateAvailable = isUpdateAvailable
            }));
            return;
        }
        
        if (await CheckForUpdate())
        {
            Logger.Setup("An update is available. Do you want to install it? (y/n)");
            if (Console.ReadKey().Key == ConsoleKey.Y)
            {
                Logger.Setup("\nDownloading and installing update...");
                await InstallUpdate();
            }
        }
        else
        {
            Logger.Setup("No updates available.");
        }
    }

    private static async Task<bool> CheckForUpdate()
    {
        string? osIdentifier = GetOsIdentifier();
        if (string.IsNullOrEmpty(osIdentifier)) return false;

        string installedVersion = GetInstalledVersion();
        (string latestVersion, _) = await GetLatestReleaseInfo(osIdentifier);

        Logger.Setup($"Installed version: {installedVersion}");
        Logger.Setup($"Latest version: {latestVersion}");
        
        if (installedVersion == "0.0.0")
        {
            Logger.Setup("No installation detected. Installing latest version...");
            await InstallServer();
            return false;
        }

        return SemverIsGreater(installedVersion, latestVersion);
    }

    private static async Task InstallUpdate()
    {
        string? osIdentifier = GetOsIdentifier();
        if (string.IsNullOrEmpty(osIdentifier)) return;

        Logger.Setup("Fetching latest release info...");
        (_, string? downloadUrl) = await GetLatestReleaseInfo(osIdentifier);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            Logger.Setup("No valid update found.");
            return;
        }
        
        Logger.Setup("Downloading update...");
        byte[] data = await HttpClient.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(AppFiles.ServerTempExePath, data);
        
        Logger.Setup("Stopping NoMercyMediaServer...");
        StopMediaServer();
        
        Logger.Setup("Installing update...");
        
        if (File.Exists(AppFiles.ServerExePath))
        {
            File.Replace(AppFiles.ServerTempExePath, AppFiles.ServerExePath, null);
        }
        else
        {
            File.Move(AppFiles.ServerTempExePath, AppFiles.ServerExePath);
        }
        
        MakeBinaryExecutable(AppFiles.ServerExePath);

        Logger.Setup("Restarting NoMercyMediaServer...");
        StartMediaServer();
        
        Logger.Setup("Update installed successfully.");
    }
    
    private static async Task InstallServer()
    {
        string? osIdentifier = GetOsIdentifier();
        if (string.IsNullOrEmpty(osIdentifier)) return;

        (_, string? downloadUrl) = await GetLatestReleaseInfo(osIdentifier);
        if (string.IsNullOrEmpty(downloadUrl)) return;

        byte[] data = await HttpClient.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(AppFiles.ServerExePath, data); // Always save to final path

        Logger.Setup("Making the binary executable...");
        MakeBinaryExecutable(AppFiles.ServerExePath);

        Logger.Setup("Starting NoMercyMediaServer...");
        StartMediaServer();

        Logger.Setup("Installation completed.");
    }
    
    private static bool SemverIsGreater(string oldVersion, string newVersion)
    {
        if (!SemVersion.TryParse(oldVersion, out SemVersion? oldSemVer)) return false;
        if (!SemVersion.TryParse(newVersion, out SemVersion? newSemVer)) return false;

        return newSemVer.ComparePrecedenceTo(oldSemVer) > 0;
    }

    private static string? GetOsIdentifier()
    {
        if (OperatingSystem.IsWindows()) return "windows-x64";
        if (OperatingSystem.IsLinux()) return "linux-x64";
        if (OperatingSystem.IsMacOS()) return "macos-x64";
        return null;
    }

    private static string GetInstalledVersion()
    {
        if (!File.Exists(AppFiles.ServerExePath)) return "0.0.0";

        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(AppFiles.ServerExePath);
        string version = versionInfo.FileVersion ?? "0.0.0";

        return version.Split('+')[0];
    }
    
    private static async Task<(string semver, string? downloadUrl)> GetLatestReleaseInfo(string? osIdentifier)
    {
        try
        {
            string response = await HttpClient.GetStringAsync(Url);
            
            Release? doc = JsonConvert.DeserializeObject<Release>(response);

            if (doc == null) return ("0.0.0", null);

            string semver = doc.TagName.StartsWith($"v") ? doc.TagName[1..] : doc.TagName;

            foreach (Asset asset in doc.Assets)
            {
                if (osIdentifier == null || !asset.Name.Contains(osIdentifier)) continue;

                return (semver, asset.DownloadUrl);
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Setup($"Failed to fetch release info: {ex.Message}");
        }

        return ("0.0.0", null);
    }

    private static void MakeBinaryExecutable(string filePath)
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
                Logger.Setup($"Failed to stop server: {ex.Message}");
            }
        }
    }

    private static void StartMediaServer()
    {
        try
        {
            Process.Start(AppFiles.ServerExePath);
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to restart server: {ex.Message}");
        }
    }
    
    private class Release
    {
        [JsonProperty("tag_name")] public string TagName { get; set; } = "v0.0.0";
        [JsonProperty("assets")] public List<Asset> Assets { get; set; } = [];
    }
    
    private class Asset
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("browser_download_url")] public string DownloadUrl { get; set; } = "";
    }
}