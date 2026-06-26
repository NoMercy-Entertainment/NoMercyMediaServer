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
            || args.Contains("--dev")
            || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is not null
            || Environment.ProcessPath?.Contains("bin\\Debug") == true
            || Environment.ProcessPath?.Contains("bin/Debug") == true;

        ShowOnStartup = args.Contains("--show") || IsDev;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}
