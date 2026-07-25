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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public ServerControlViewModel ServerControlViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public StartupArgumentsViewModel StartupArgumentsViewModel { get; }
    public LogViewerViewModel LogViewerViewModel { get; }

    public int SelectedTabIndex
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel(ServerConnection serverConnection, ServerProcessLauncher processLauncher)
    {
        ServerControlViewModel = new(serverConnection, processLauncher);
        SettingsViewModel = new(serverConnection);
        StartupArgumentsViewModel = new();
        LogViewerViewModel = new(serverConnection);

        ServerControlViewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(ServerControlViewModel.IsServerRunning))
            {
                SettingsViewModel.IsServerRunning = ServerControlViewModel.IsServerRunning;

                if (ServerControlViewModel.IsServerRunning && !SettingsViewModel.ConfigLoaded)
                    await SettingsViewModel.LoadConfigAsync();
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}
