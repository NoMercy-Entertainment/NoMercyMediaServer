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
using NoMercy.Launcher.Models;

namespace NoMercy.Launcher.Views;

public partial class ActiveSessionDialog : Window
{
    private bool _interrupted;

    public ActiveSessionDialog()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window owner, ActivityInfo activity)
    {
        ActiveSessionDialog dialog = new();
        dialog.Configure(activity: activity);
        await dialog.ShowDialog(owner: owner);
        return dialog._interrupted;
    }

    private void Configure(ActivityInfo activity)
    {
        List<string> parts = [];

        if (activity.ActiveStreams > 0)
            parts.Add(
                item: $"{activity.ActiveStreams} active stream{(activity.ActiveStreams == 1 ? "" : "s")} — interrupting will stop playback for those users"
            );

        if (activity.ActiveEncodes > 0)
            parts.Add(
                item: $"{activity.ActiveEncodes} active encode{(activity.ActiveEncodes == 1 ? "" : "s")} — these will resume where they left off"
            );

        ActivitySummary.Text = string.Join(separator: "\n", values: parts);

        ResumeNote.Text =
            activity.ActiveEncodes > 0
                ? "Active encodes will resume where they left off."
                : string.Empty;
    }

    private void OnWaitClick(object? sender, RoutedEventArgs e)
    {
        _interrupted = false;
        Close();
    }

    private void OnInterruptClick(object? sender, RoutedEventArgs e)
    {
        _interrupted = true;
        Close();
    }
}
