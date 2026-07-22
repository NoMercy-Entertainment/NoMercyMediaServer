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

using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Launcher.Services;

/// <summary>
/// <see cref="LauncherSettings"/> reads/writes real JSON under
/// <see cref="AppFiles.TraySettingsFile"/>. TestEnvironmentSetup routes
/// AppFiles.AppPath at an isolated per-process temp root (NOMERCY_APP_PATH),
/// so exercising the real file system here never touches a developer's actual
/// tray settings.
/// </summary>
public sealed class LauncherSettingsTests : IDisposable
{
    public LauncherSettingsTests()
    {
        if (File.Exists(path: AppFiles.TraySettingsFile))
            File.Delete(path: AppFiles.TraySettingsFile);
    }

    public void Dispose()
    {
        if (File.Exists(path: AppFiles.TraySettingsFile))
            File.Delete(path: AppFiles.TraySettingsFile);
    }

    [Fact]
    public void Load_NoFileOnDisk_ReturnsDefaults()
    {
        TraySettings settings = LauncherSettings.Load();

        settings.ShowOnStartup.Should().BeFalse();
        settings.StartupArguments.Should().Be(expected: string.Empty);
        settings.AutoStart.Should().BeFalse();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsEveryField()
    {
        TraySettings original = new()
        {
            ShowOnStartup = true,
            StartupArguments = "--dev --port 7626",
            AutoStart = true,
        };

        LauncherSettings.Save(settings: original);
        TraySettings loaded = LauncherSettings.Load();

        loaded.ShowOnStartup.Should().BeTrue();
        loaded.StartupArguments.Should().Be(expected: "--dev --port 7626");
        loaded.AutoStart.Should().BeTrue();
    }

    [Fact]
    public void Save_CreatesConfigDirectoryWhenMissing()
    {
        string? directory = Path.GetDirectoryName(path: AppFiles.TraySettingsFile);
        directory.Should().NotBeNull();

        if (Directory.Exists(path: directory) && directory != AppFiles.AppPath)
        {
            // Only remove the leaf config directory itself, never AppPath.
            foreach (string file in Directory.GetFiles(path: directory!))
                File.Delete(path: file);
        }

        LauncherSettings.Save(settings: new() { AutoStart = true });

        File.Exists(path: AppFiles.TraySettingsFile).Should().BeTrue();
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsInsteadOfThrowing()
    {
        string? directory = Path.GetDirectoryName(path: AppFiles.TraySettingsFile);
        if (directory is not null && !Directory.Exists(path: directory))
            Directory.CreateDirectory(path: directory);

        File.WriteAllText(path: AppFiles.TraySettingsFile, contents: "{ not valid json ");

        TraySettings settings = LauncherSettings.Load();

        settings.ShowOnStartup.Should().BeFalse();
        settings.StartupArguments.Should().Be(expected: string.Empty);
    }
}
