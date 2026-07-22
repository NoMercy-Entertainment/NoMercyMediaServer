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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher;

public class App : Application
{
    private TrayIconManager? _trayIconManager;
    private ServerConnection? _serverConnection;
    private ServerProcessLauncher? _processLauncher;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(obj: this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serverConnection = new();
        _processLauncher = new();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _trayIconManager = new(
                serverConnection: _serverConnection,
                processLauncher: _processLauncher,
                lifetime: desktop,
                showOnStartup: Program.ShowOnStartup,
                isDev: Program.IsDev
            );
            _trayIconManager.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
