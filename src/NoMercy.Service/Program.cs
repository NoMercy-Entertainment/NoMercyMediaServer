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
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Plugins.Abstractions;
using NoMercy.Service.Configuration;
using NoMercy.Service.Hosting;
using NoMercy.Service.Seeds;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;
using NoMercy.Setup.Ui;
using NoMercy.Storage;
using NoMercyQueue;


namespace NoMercy.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Resolve renamed OpenSSL DLL for installer deployments where
        // libcrypto-3-x64.dll is renamed to nmossl-3-x64.dll to avoid
        // file locks from other applications on the system.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
            {
                if (!name.Contains("libcrypto", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                string renamed = Path.Combine(AppContext.BaseDirectory, "nmossl-3-x64.dll");
                if (File.Exists(renamed) && NativeLibrary.TryLoad(renamed, out IntPtr handle))
                    return handle;

                return IntPtr.Zero;
            };
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = (Exception)eventArgs.ExceptionObject;
            Logger.App("UnhandledException " + exception);
        };

        // Tasks that lose their last exception observer (fire-and-forget
        // patterns, GC'd before await) raise here. Marking them observed
        // keeps them from escalating to UnhandledException. async-void
        // chains aren't covered by this — those are handled defensively
        // at the source (see ChromeCastService.NeutralizeTimer).
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.App("UnobservedTaskException " + e.Exception);
            e.SetObserved();
        };

        await Parser
            .Default.ParseArguments<StartupOptions>(args)
            .MapResult(o => new ServerBootstrapper().RunAsync(o), ErrorParsingArguments);

        static Task ErrorParsingArguments(IEnumerable<Error> errors)
        {
            Environment.ExitCode = 1;
            return Task.CompletedTask;
        }
    }
}
