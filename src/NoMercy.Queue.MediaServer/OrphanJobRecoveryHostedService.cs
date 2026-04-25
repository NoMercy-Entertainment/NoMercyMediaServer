using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Phase 4.14 — startup orphan-job recovery. On boot, scans the queue for
/// jobs left in "running" state (<c>ReservedAt != null</c>) by a previous
/// shutdown that are older than the cutoff window. Each orphan that has
/// already been retried at least once is moved to <c>FailedJobs</c> with
/// <c>job.interrupted_no_checkpoint</c> so the user gets a clear failure
/// instead of an endless retry loop. First-time orphans (Attempts == 0)
/// are left for <c>QueueRunner.ResetAllReservedJobs</c> to retry once
/// cleanly.
///
/// Future enhancement: scan checkpoint files by JobId to distinguish
/// "resumable" orphans from truly-broken ones, regardless of attempt
/// count.
/// </summary>
public class OrphanJobRecoveryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrphanJobRecoveryHostedService> logger
) : IHostedService
{
    private static readonly TimeSpan OrphanCutoff = TimeSpan.FromSeconds(30);
    private const string InterruptedReason = "job.interrupted_no_checkpoint";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IQueueContext context = scope.ServiceProvider.GetRequiredService<IQueueContext>();

            DateTime cutoff = DateTime.UtcNow.Subtract(OrphanCutoff);
            IReadOnlyList<QueueJobModel> orphans = context.GetReservedJobsOlderThan(cutoff);

            if (orphans.Count == 0)
            {
                logger.LogDebug("Orphan recovery: no orphan jobs found");
                return Task.CompletedTask;
            }

            int failed = 0;
            int requeued = 0;
            foreach (QueueJobModel orphan in orphans)
            {
                if (orphan.Attempts > 0)
                {
                    context.AddFailedJob(
                        new()
                        {
                            Uuid = Guid.NewGuid(),
                            Connection = "default",
                            Queue = orphan.Queue,
                            Payload = orphan.Payload,
                            Exception = InterruptedReason,
                            FailedAt = DateTime.UtcNow,
                        }
                    );
                    context.RemoveJob(orphan);
                    failed++;
                }
                else
                {
                    requeued++;
                }
            }

            context.SaveChanges();

            logger.LogInformation(
                "Orphan recovery: scanned {Total} orphan job(s); {Failed} moved to FailedJobs ({Reason}); {Requeued} left for retry",
                orphans.Count,
                failed,
                InterruptedReason,
                requeued
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Orphan recovery failed; continuing startup");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
