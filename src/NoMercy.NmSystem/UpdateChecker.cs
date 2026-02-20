using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem;

public static class UpdateChecker
{
    private static readonly HttpClient HttpClient = new();
    private const string GithubReleasesUrl = "https://api.github.com/repos/NoMercy-Entertainment/NoMercyMediaServer/releases/latest";

    static UpdateChecker()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
    }

    public static Task StartPeriodicUpdateCheck()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                bool available = await IsUpdateAvailableAsync();
                Config.UpdateAvailable = available;
                await Task.Delay(TimeSpan.FromHours(6));
            }

            // ReSharper disable once FunctionNeverReturns
        });

        return Task.CompletedTask;
    }

    public static async Task<bool> IsUpdateAvailableAsync()
    {
        try
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(GithubReleasesUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            LatestReleaseInfo? release = JsonConvert.DeserializeObject<LatestReleaseInfo>(json);

            if (release is null || string.IsNullOrEmpty(release.TagName))
                return false;

            string latestVersion = release.TagName.StartsWith("v")
                ? release.TagName[1..]
                : release.TagName;

            string currentVersion = Software.GetReleaseVersion();

            Config.LatestVersion = latestVersion;

            if (string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                Config.RestartNeeded = false;
                return false;
            }

            string? onDiskVersion = Software.GetFileVersion(AppFiles.ServerExePath);

            // Also check the installed binary (e.g. Program Files) if available
            if (onDiskVersion is null || !string.Equals(latestVersion, onDiskVersion, StringComparison.OrdinalIgnoreCase))
            {
                string? installDir = Environment.GetEnvironmentVariable("NOMERCY_INSTALL_DIR");
                if (!string.IsNullOrEmpty(installDir))
                {
                    string installedExe = Path.Combine(installDir, "NoMercyMediaServer" + Information.Info.ExecSuffix);
                    string? installedVersion = Software.GetFileVersion(installedExe);
                    if (installedVersion is not null &&
                        string.Equals(latestVersion, installedVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        onDiskVersion = installedVersion;
                    }
                }
            }

            Config.RestartNeeded = onDiskVersion is not null &&
                                   string.Equals(latestVersion, onDiskVersion, StringComparison.OrdinalIgnoreCase);

            if (Version.TryParse(latestVersion, out Version? latest) &&
                Version.TryParse(currentVersion, out Version? current))
            {
                return latest > current;
            }

            return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            Logger.Setup($"Update check failed: {e.Message}", LogEventLevel.Debug);
            return false;
        }
    }

    private class LatestReleaseInfo
    {
        [JsonProperty("tag_name")] public string TagName { get; set; } = string.Empty;
    }
}
