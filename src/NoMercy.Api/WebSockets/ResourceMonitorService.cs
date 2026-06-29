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
using NoMercy.Monitoring;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

using Microsoft.Extensions.Logging;
namespace NoMercy.Api.WebSockets;

public class ResourceMonitorService(
    ILogger<ResourceMonitorService> logger,
    ResourceMonitor resourceMonitor,
    IClientMessenger clientMessenger
) : IResourceMonitorService
{
    private readonly Lock _sync = new();
    private bool _broadcasting;
    private CancellationTokenSource? _cancellationTokenSource;

    public int ActiveSubscribers => _broadcasting ? 1 : 0;

    public void Start()
    {
        lock (_sync)
        {
            if (_broadcasting)
                return;
            _broadcasting = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new();
        }

        logger.LogInformation("Starting resource monitoring broadcast");
        CancellationToken token = _cancellationTokenSource.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await BroadcastLoop(token);
            }
            catch (Exception ex)
            {
                logger.LogError("Resource monitor broadcast loop crashed: {Message}", ex.Message);
            }
        });
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_broadcasting)
                return;
            _broadcasting = false;
        }

        logger.LogInformation("Stopping resource monitoring broadcast");
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS already disposed — nothing to cancel.
        }
    }

    private async Task BroadcastLoop(CancellationToken cancellationToken)
    {
        while (_broadcasting && !cancellationToken.IsCancellationRequested)
        {
            DateTime time = DateTime.Now;
            try
            {
                Resource resourceData = resourceMonitor.Monitor();
                await clientMessenger.SendToAll("ResourceUpdate", "dashboardHub", resourceData);
                int delay = 1000 - (int)(DateTime.Now - time).TotalMilliseconds;
                if (delay > 0)
                    await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError("Error broadcasting resource data: {Message}", e.Message);
            }
        }
    }
}
