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
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Service.Hosting;

public static class HostLifecycleHooks
{
    public static void Register(WebApplication app, Stopwatch stopWatch)
    {
        app.Services.GetService<IHostApplicationLifetime>()
            ?.ApplicationStarted.Register(callback: () =>
            {
                app.Services.GetRequiredService<IBootStatus>().MarkStarted();
                stopWatch.Stop();
            });

        app.Services.GetService<IHostApplicationLifetime>()
            ?.ApplicationStopping.Register(callback: () =>
            {
                Logger.App(message: "Application is shutting down...");
            });
    }
}
