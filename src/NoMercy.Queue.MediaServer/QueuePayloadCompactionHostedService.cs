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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Runs the one-time payload compaction in the background after boot.
/// <para>
/// Safe to run alongside the workers rather than ahead of them: a row this has
/// not reached yet still carries its own input and runs from that, so the pass
/// only ever returns space. It races nothing.
/// </para>
/// </summary>
public class QueuePayloadCompactionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<QueuePayloadCompactionHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Off the startup path — a first run on a large backlog rewrites every row.
        await Task.Yield();

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            QueuePayloadCompaction compaction =
                scope.ServiceProvider.GetRequiredService<QueuePayloadCompaction>();

            await compaction.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down. The next boot picks up where this left off, because
            // what is left to do is "rows with no payload hash".
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Queue payload compaction failed");
        }
    }
}
