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

public partial class ServerControlView : UserControl
{
    public ServerControlView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ServerControlViewModel? ViewModel => DataContext as ServerControlViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.ShowActiveSessionDialog = async activity =>
        {
            Window? owner = TopLevel.GetTopLevel(visual: this) as Window;
            if (owner is null)
                return false;

            return await ActiveSessionDialog.ShowAsync(owner: owner, activity: activity);
        };
    }

    /// <summary>
    /// Wrap an async-void click handler so an exception from the awaited
    /// view-model task lands in LauncherLog instead of crashing the
    /// launcher via the AppDomain unhandled-exception path.
    /// </summary>
    private static async void SafeRun(string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LauncherLog.Error(message: $"ServerControlView.{label} failed: {ex.Message}", ex: ex);
        }
    }

    private void OnOpenAppClick(object? sender, RoutedEventArgs e) =>
        SafeRun(label: nameof(OnOpenAppClick), action: () => ViewModel?.LaunchAppAsync() ?? Task.CompletedTask);

    private void OnStartClick(object? sender, RoutedEventArgs e) =>
        SafeRun(label: nameof(OnStartClick), action: () => ViewModel?.StartServerAsync() ?? Task.CompletedTask);

    private void OnStopClick(object? sender, RoutedEventArgs e) =>
        SafeRun(label: nameof(OnStopClick), action: () => ViewModel?.StopServerAsync() ?? Task.CompletedTask);

    private void OnRestartClick(object? sender, RoutedEventArgs e) =>
        SafeRun(
            label: nameof(OnRestartClick),
            action: () => ViewModel?.RestartServerAsync() ?? Task.CompletedTask
        );

    private void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        SafeRun(
            label: nameof(OnRefreshClick),
            action: () => ViewModel?.RefreshStatusAsync() ?? Task.CompletedTask
        );

    private void OnApplyUpdate(object? sender, RoutedEventArgs e) =>
        SafeRun(label: nameof(OnApplyUpdate), action: () => ViewModel?.ApplyUpdateAsync() ?? Task.CompletedTask);

    private void OnAutoStartToggle(object? sender, RoutedEventArgs e) =>
        SafeRun(
            label: nameof(OnAutoStartToggle),
            action: () =>
                ViewModel is not null && sender is CheckBox checkBox
                    ? ViewModel.ToggleAutoStartAsync(enabled: checkBox.IsChecked == true)
                    : Task.CompletedTask
        );
}
