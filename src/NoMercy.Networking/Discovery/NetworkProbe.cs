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
    private static readonly string[] ProbeTargets = ["api.nomercy.tv", "1.1.1.1", "8.8.8.8"];

    public static async Task<bool> CheckConnectivity(int timeoutMs = 3000)
    {
        foreach (string target in ProbeTargets)
        {
            try
            {
                using TcpClient client = new();
                Task connectTask = client.ConnectAsync(target, 443);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask)
                {
                    await connectTask;
                    return true;
                }
            }
            catch
            {
                // Try next target
            }
        }

        Logger.Setup("No network connectivity detected", LogEventLevel.Warning);
        return false;
    }
}
