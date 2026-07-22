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
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher.ViewModels;

public class StartupArgumentsViewModel : INotifyPropertyChanged
{
    public string StartupArguments
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public string SaveStatus
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public Task LoadAsync()
    {
        TraySettings settings = LauncherSettings.Load();
        StartupArguments = settings.StartupArguments;
        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        TraySettings settings = LauncherSettings.Load();
        settings.StartupArguments = StartupArguments;
        LauncherSettings.Save(settings: settings);
        SaveStatus = "Saved";
        return Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(sender: this, e: new(propertyName: propertyName));
    }
}
