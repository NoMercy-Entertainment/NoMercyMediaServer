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

using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// <see cref="MainViewModel"/> composes the four tab view models and wires
/// <c>ServerControlViewModel.IsServerRunning</c> changes into
/// <c>SettingsViewModel.IsServerRunning</c> — this pins that composition and
/// the tab-selection property, without the (separate, IPC-driven) config
/// auto-load side effect that fires when the server transitions to running
/// (covered indirectly by <see cref="SettingsViewModelTests"/>'s LoadConfigAsync
/// tests, since MainViewModel just forwards to that same method).
/// </summary>
public sealed class MainViewModelTests
{
    private static MainViewModel CreateViewModel() =>
        new(serverConnection: new ServerConnection(pipeNameOrSocketPath: $"nomercy-test-{Guid.NewGuid():N}"), processLauncher: new ServerProcessLauncher());

    [Fact]
    public void Constructor_CreatesAllFourChildViewModels()
    {
        MainViewModel viewModel = CreateViewModel();

        viewModel.ServerControlViewModel.Should().NotBeNull();
        viewModel.SettingsViewModel.Should().NotBeNull();
        viewModel.StartupArgumentsViewModel.Should().NotBeNull();
        viewModel.LogViewerViewModel.Should().NotBeNull();
    }

    [Fact]
    public void SelectedTabIndex_PropertyChanged_FiresOnAssignment()
    {
        MainViewModel viewModel = CreateViewModel();
        List<string> changed = [];
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changed.Add(item: e.PropertyName);
        };

        viewModel.SelectedTabIndex = 1;

        viewModel.SelectedTabIndex.Should().Be(expected: 1);
        changed.Should().Contain(expected: nameof(MainViewModel.SelectedTabIndex));
    }

    [Fact]
    public void ServerControlViewModel_IsServerRunningChange_ForwardsToSettingsViewModel()
    {
        MainViewModel viewModel = CreateViewModel();

        viewModel.ServerControlViewModel.IsServerRunning = true;

        viewModel.SettingsViewModel.IsServerRunning.Should().BeTrue();
    }

    [Fact]
    public void ServerControlViewModel_IsServerRunningSetFalse_ForwardsFalseToSettingsViewModel()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.ServerControlViewModel.IsServerRunning = true;

        viewModel.ServerControlViewModel.IsServerRunning = false;

        viewModel.SettingsViewModel.IsServerRunning.Should().BeFalse();
    }
}
