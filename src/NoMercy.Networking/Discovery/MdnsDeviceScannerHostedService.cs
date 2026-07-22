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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercy.Networking.Discovery;

public sealed class MdnsDeviceScannerHostedService(
    MdnsDeviceScanner scanner,
    ILogger<MdnsDeviceScannerHostedService> logger
) : BackgroundService
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(seconds: 30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        scanner.Start(stoppingToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                scanner.Probe();
                await Task.Delay(delay: ProbeInterval, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // mDNS multicast can flake when interfaces flip (VPN connect,
                // hotspot toggle, NIC sleep). Don't let a probe failure tear
                // down the host — log and retry on the next interval.
                logger.LogWarning(exception: ex, message: "mDNS probe failed; will retry");
                try
                {
                    await Task.Delay(delay: ProbeInterval, cancellationToken: stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
