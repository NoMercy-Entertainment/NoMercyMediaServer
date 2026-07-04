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
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Phase 4.14 — startup orphan-job recovery. On boot, scans the queue for
/// jobs left in "running" state (<c>ReservedAt != null</c>) by a previous
/// shutdown that are older than the cutoff window.
///
/// Checkpoint-aware triage (Phase 4.14b): encoder-queue orphans that have a
/// crash checkpoint are re-queued with Attempts=0 so the resume path gets a
/// chance to pick up from the last-known keyframe position. Orphans with no
/// checkpoint follow the original logic: a genuine first-time orphan (reserved
/// exactly once — <c>Attempts &lt;= 1</c>, since <c>ReserveJob</c> increments
/// Attempts in the same write that sets ReservedAt) gets one clean retry with
/// its reservation released and its attempt budget refunded; an orphan that
/// has already burned a prior attempt beyond that single reservation
/// (<c>Attempts &gt; 1</c>) is a repeat offender and moves to FailedJobs so it
/// can't retry forever.
/// </summary>
public class OrphanJobRecoveryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrphanJobRecoveryHostedService> logger
) : BackgroundService
{
    private static readonly TimeSpan OrphanCutoff = TimeSpan.FromSeconds(30);
    private const string InterruptedReason = "job.interrupted_no_checkpoint";
    private static readonly string[] EncoderQueues =
    [
        QueueNames.Encoder,
        QueueNames.EncoderGpu,
        QueueNames.EncoderCpu,
    ];

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Run off the startup path so a slow/large recovery sweep never blocks
        // the host from coming up.
        await Task.Yield();

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IQueueContext context = scope.ServiceProvider.GetRequiredService<IQueueContext>();
            IOrphanCheckpointLookup? checkpointLookup =
                scope.ServiceProvider.GetService<IOrphanCheckpointLookup>();

            DateTime cutoff = DateTime.UtcNow.Subtract(OrphanCutoff);
            IReadOnlyList<QueueJobModel> orphans = context.GetReservedJobsOlderThan(cutoff);

            if (orphans.Count == 0)
            {
                logger.LogDebug("Orphan recovery: no orphan jobs found");
                return;
            }

            int failed = 0;
            int requeued = 0;
            int resumable = 0;

            foreach (QueueJobModel orphan in orphans)
            {
                bool isEncoderJob = EncoderQueues.Contains(orphan.Queue);

                if (isEncoderJob && checkpointLookup is not null)
                {
                    bool hasCheckpoint = await checkpointLookup.HasCheckpointAsync(
                        orphan.Payload,
                        cancellationToken
                    );

                    if (hasCheckpoint)
                    {
                        orphan.Attempts = 0;
                        orphan.ReservedAt = null;
                        context.UpdateJob(orphan);
                        resumable++;
                        continue;
                    }
                }

                if (orphan.Attempts > 1)
                {
                    context.AddFailedJobAndRemoveJob(
                        new()
                        {
                            Uuid = Guid.NewGuid(),
                            Connection = "default",
                            Queue = orphan.Queue,
                            Payload = orphan.Payload,
                            Exception = InterruptedReason,
                            FailedAt = DateTime.UtcNow,
                        },
                        orphan
                    );
                    failed++;
                }
                else
                {
                    orphan.Attempts = (byte)Math.Max(0, orphan.Attempts - 1);
                    orphan.ReservedAt = null;
                    context.UpdateJob(orphan);
                    requeued++;
                }
            }

            context.SaveChanges();

            logger.LogInformation(
                "Orphan recovery: scanned {Total} orphan job(s); {Failed} moved to FailedJobs ({Reason}); {Requeued} left for retry; {Resumable} re-queued for checkpoint resume",
                orphans.Count,
                failed,
                InterruptedReason,
                requeued,
                resumable
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Orphan recovery failed; continuing startup");
        }

        return;
    }
}
