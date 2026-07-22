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
using NoMercy.Launcher.ViewModels;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// <see cref="StartupArgumentsViewModel"/> round-trips through the real
/// <see cref="LauncherSettings"/> JSON file (test-isolated by
/// TestEnvironmentSetup's NOMERCY_APP_PATH) — no mocking needed since the
/// underlying settings file lives in a per-process temp root during tests.
/// </summary>
public sealed class StartupArgumentsViewModelTests : IDisposable
{
    public StartupArgumentsViewModelTests()
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
    public async Task LoadAsync_NoSavedSettings_LeavesStartupArgumentsEmpty()
    {
        StartupArgumentsViewModel viewModel = new();

        await viewModel.LoadAsync();

        viewModel.StartupArguments.Should().Be(expected: string.Empty);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsStartupArguments()
    {
        StartupArgumentsViewModel saver = new() { StartupArguments = "--dev --port 7626" };
        await saver.SaveAsync();

        StartupArgumentsViewModel loader = new();
        await loader.LoadAsync();

        loader.StartupArguments.Should().Be(expected: "--dev --port 7626");
    }

    [Fact]
    public async Task SaveAsync_SetsSaveStatusToSaved()
    {
        StartupArgumentsViewModel viewModel = new() { StartupArguments = "--dev" };

        await viewModel.SaveAsync();

        viewModel.SaveStatus.Should().Be(expected: "Saved");
    }

    [Fact]
    public async Task SaveAsync_PreservesOtherTraySettingsFields()
    {
        LauncherSettings.Save(settings: new TraySettings { ShowOnStartup = true, AutoStart = true });

        StartupArgumentsViewModel viewModel = new() { StartupArguments = "--dev" };
        await viewModel.SaveAsync();

        TraySettings reloaded = LauncherSettings.Load();
        reloaded.ShowOnStartup.Should().BeTrue();
        reloaded.AutoStart.Should().BeTrue();
        reloaded.StartupArguments.Should().Be(expected: "--dev");
    }

    [Fact]
    public void PropertyChanged_FiresOnStartupArgumentsAndSaveStatusChange()
    {
        StartupArgumentsViewModel viewModel = new();
        List<string> changed = [];
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changed.Add(item: e.PropertyName);
        };

        viewModel.StartupArguments = "--dev";
        viewModel.SaveStatus = "Saved";

        changed.Should().Contain(expected: nameof(StartupArgumentsViewModel.StartupArguments));
        changed.Should().Contain(expected: nameof(StartupArgumentsViewModel.SaveStatus));
    }
}
