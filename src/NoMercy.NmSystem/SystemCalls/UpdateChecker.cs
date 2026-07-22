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
            name: "User-Agent",
            value: ExternalServicesConfig.Current.UserAgent
        );
    }

    public async Task<bool> IsUpdateAvailableAsync()
    {
        try
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(requestUri: GithubReleasesUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            LatestReleaseInfo? release = JsonConvert.DeserializeObject<LatestReleaseInfo>(value: json);

            if (release is null || string.IsNullOrEmpty(value: release.TagName))
                return false;

            string latestVersion = release.TagName.StartsWith(value: "v")
                ? release.TagName[1..]
                : release.TagName;

            string currentVersion = Software.GetReleaseVersion();

            updateStatus.LatestVersion = latestVersion;

            if (string.Equals(a: latestVersion, b: currentVersion, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                updateStatus.RestartNeeded = false;
                updateStatus.UpdateAvailable = false;

                return false;
            }

            // LOCAL-ONLY: UpdateChecker lives in NmSystem; no reference to NoMercy.Providers.
            string? onDiskVersion = Software.GetFileVersion(
                driver: new LocalStorageDriver(),
                exePath: AppFiles.ServerExePath
            );

            // Also check the installed binary (e.g. Program Files) if available
            if (
                onDiskVersion is null
                || !string.Equals(a: latestVersion, b: onDiskVersion, comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            {
                string? installDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");

                if (!string.IsNullOrEmpty(value: installDir))
                {
                    string installedExe = Path.Combine(
                        path1: installDir,
                        path2: "NoMercyMediaServer" + Info.ExecSuffix
                    );

                    string? installedVersion = Software.GetFileVersion(
                        driver: new LocalStorageDriver(),
                        exePath: installedExe
                    );

                    if (
                        installedVersion is not null
                        && string.Equals(
                            a: latestVersion,
                            b: installedVersion,
                            comparisonType: StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        onDiskVersion = installedVersion;
                    }
                }
            }

            updateStatus.RestartNeeded =
                onDiskVersion is not null
                && string.Equals(a: latestVersion, b: onDiskVersion, comparisonType: StringComparison.OrdinalIgnoreCase);

            bool updateAvailable;

            if (
                Version.TryParse(input: latestVersion, result: out Version? latest)
                && Version.TryParse(input: currentVersion, result: out Version? current)
            )
            {
                updateAvailable = latest > current;
            }
            else
            {
                updateAvailable = !string.Equals(
                    a: latestVersion,
                    b: currentVersion,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                );
            }

            updateStatus.UpdateAvailable = updateAvailable;

            return updateAvailable;
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"Update check failed: {e.Message}", level: LogEventLevel.Debug);

            return false;
        }
    }

    private class LatestReleaseInfo
    {
        [JsonProperty(propertyName: "tag_name")]
        public string TagName { get; set; } = string.Empty;
    }
}
