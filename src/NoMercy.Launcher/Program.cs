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

using System.Diagnostics;
using Avalonia;

namespace NoMercy.Launcher;

public static class Program
{
    public static bool ShowOnStartup { get; private set; }
    public static bool IsDev { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        IsDev =
            Debugger.IsAttached
            || args.Contains(value: "--dev")
            || Environment.GetEnvironmentVariable(variable: "DOTNET_RUNNING_IN_CONTAINER") is not null
            || Environment.ProcessPath?.Contains(value: "bin\\Debug") == true
            || Environment.ProcessPath?.Contains(value: "bin/Debug") == true;

        ShowOnStartup = args.Contains(value: "--show") || IsDev;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args: args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}
