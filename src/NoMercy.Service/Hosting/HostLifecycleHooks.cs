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
using NoMercy.NmSystem.Status;
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

namespace NoMercy.Service.Hosting;

public static class HostLifecycleHooks
{
    public static void Register(WebApplication app, Stopwatch stopWatch)
    {
        app.Services.GetService<IHostApplicationLifetime>()
            ?.ApplicationStarted.Register(() =>
            {
                app.Services.GetRequiredService<IBootStatus>().MarkStarted();
                stopWatch.Stop();
            });

        app.Services.GetService<IHostApplicationLifetime>()
            ?.ApplicationStopping.Register(() =>
            {
                Logger.App("Application is shutting down...");
            });
    }
}
