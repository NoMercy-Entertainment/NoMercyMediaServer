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

using System.Net.Sockets;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Discovery;

public static class NetworkProbe
{
    public static string[] ProbeTargets { get; set; } = ["api.nomercy.tv", "1.1.1.1", "8.8.8.8"];

    public static async Task<bool> CheckConnectivity(int timeoutMs = 3000)
    {
        foreach (string target in ProbeTargets)
        {
            try
            {
                using TcpClient client = new();
                using CancellationTokenSource cts = new(millisecondsDelay: timeoutMs);
                await client.ConnectAsync(host: target, port: 443, cancellationToken: cts.Token);
                return true;
            }
            catch
            {
                // Timed out or unreachable — try the next target
            }
        }

        Logger.Setup(message: "No network connectivity detected", level: LogEventLevel.Warning);
        return false;
    }
}
