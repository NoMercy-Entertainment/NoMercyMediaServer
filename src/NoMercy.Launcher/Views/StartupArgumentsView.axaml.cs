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

using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class StartupArgumentsView : UserControl
{
    public StartupArgumentsView()
    {
        InitializeComponent();
    }

    private StartupArgumentsViewModel? ViewModel => DataContext as StartupArgumentsViewModel;

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not null)
                await ViewModel.SaveAsync();
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"StartupArgumentsView.OnSaveClick failed: {ex.Message}", ex);
        }
    }
}
