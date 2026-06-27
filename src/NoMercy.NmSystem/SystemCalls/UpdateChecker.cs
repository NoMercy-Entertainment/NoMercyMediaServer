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

using Newtonsoft.Json;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.Storage.Drivers.Local;
using Serilog.Events;

namespace NoMercy.NmSystem.SystemCalls;

public interface IUpdateChecker
{
    Task<bool> IsUpdateAvailableAsync();
}

public class UpdateChecker(IUpdateStatus updateStatus) : IUpdateChecker
{
    private static readonly HttpClient HttpClient = new();

    private const string GithubReleasesUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest";

    static UpdateChecker()
    {
        HttpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            ExternalServicesConfig.Current.UserAgent
        );
    }

    public async Task<bool> IsUpdateAvailableAsync()
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

            updateStatus.LatestVersion = latestVersion;

            if (string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                updateStatus.RestartNeeded = false;
                updateStatus.UpdateAvailable = false;

                return false;
            }

            // LOCAL-ONLY: UpdateChecker lives in NmSystem; no reference to NoMercy.Providers.
            string? onDiskVersion = Software.GetFileVersion(
                new LocalStorageDriver(),
                AppFiles.ServerExePath
            );

            // Also check the installed binary (e.g. Program Files) if available
            if (
                onDiskVersion is null
                || !string.Equals(latestVersion, onDiskVersion, StringComparison.OrdinalIgnoreCase)
            )
            {
                string? installDir = Environment.GetEnvironmentVariable("NOMERCY_INSTALL_DIR");

                if (!string.IsNullOrEmpty(installDir))
                {
                    string installedExe = Path.Combine(
                        installDir,
                        "NoMercyMediaServer" + Info.ExecSuffix
                    );

                    string? installedVersion = Software.GetFileVersion(
                        new LocalStorageDriver(),
                        installedExe
                    );

                    if (
                        installedVersion is not null
                        && string.Equals(
                            latestVersion,
                            installedVersion,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        onDiskVersion = installedVersion;
                    }
                }
            }

            updateStatus.RestartNeeded =
                onDiskVersion is not null
                && string.Equals(latestVersion, onDiskVersion, StringComparison.OrdinalIgnoreCase);

            bool updateAvailable;

            if (
                Version.TryParse(latestVersion, out Version? latest)
                && Version.TryParse(currentVersion, out Version? current)
            )
            {
                updateAvailable = latest > current;
            }
            else
            {
                updateAvailable = !string.Equals(
                    latestVersion,
                    currentVersion,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            updateStatus.UpdateAvailable = updateAvailable;

            return updateAvailable;
        }
        catch (Exception e)
        {
            Logger.Setup($"Update check failed: {e.Message}", LogEventLevel.Debug);

            return false;
        }
    }

    private class LatestReleaseInfo
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; } = string.Empty;
    }
}
