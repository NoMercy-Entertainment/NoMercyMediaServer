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

using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Outcome of a single <see cref="OrphanRecoveryTriage.RunAsync"/> pass.
/// </summary>
public readonly record struct OrphanTriageResult(int Failed, int Requeued, int Resumable);

/// <summary>
/// The single source of truth for orphan-job triage: given a batch of jobs
/// whose <c>ReservedAt</c> is older than a caller-chosen cutoff, decides for
/// each one whether it is checkpoint-resumable, deserves one more clean retry,
/// or has burned its budget and must dead-letter.
///
/// <para>Both <see cref="OrphanJobRecoveryHostedService"/> (one-shot, on boot)
/// and <see cref="StuckReservationReaperHostedService"/> (periodic, while the
/// server is running) call this same method so the recovery decision never
/// forks into two divergent implementations. The two callers differ in one
/// thing only — <paramref name="refundAttemptOnRequeue"/> — because they face
/// different convergence requirements; see that parameter's doc.</para>
/// </summary>
public static class OrphanRecoveryTriage
{
    public const string InterruptedReason = "job.interrupted_no_checkpoint";

    /// <summary>
    /// Triages <paramref name="orphans"/> in place and persists the outcome via
    /// <paramref name="context"/>. <paramref name="encoderQueues"/> identifies
    /// which of the orphan's <c>Queue</c> values are encoder queues — only
    /// those consult <paramref name="checkpointLookup"/> for a resumable crash
    /// checkpoint; every other queue follows the plain attempt-budget path.
    /// </summary>
    /// <param name="refundAttemptOnRequeue">
    /// When a non-dead-lettered orphan is requeued (the <c>Attempts &lt;= 1</c>
    /// branch), whether to refund the attempt it burned by decrementing
    /// <c>Attempts</c> back down (the boot-pass behavior: a crash gets exactly
    /// one free clean retry, since nothing else could have caused that
    /// reservation). The periodic reaper must NOT refund — <c>ReserveJob</c>
    /// only ever increments <c>Attempts</c> on reservation, so refunding on
    /// every periodic pass would hold a repeatedly-hanging job at
    /// <c>Attempts == 1</c> forever: it never crosses the
    /// <c>Attempts &gt; 1</c> dead-letter threshold below and re-duplicates
    /// its work every reaper interval without bound. Leaving <c>Attempts</c>
    /// untouched here means the NEXT reservation's normal increment carries it
    /// over that threshold within a couple of reclaims.
    /// </param>
    /// <param name="deadLetterReasonFactory">
    /// Produces the <c>FailedJobModel.Exception</c> string for a dead-lettered
    /// orphan. Defaults to <see cref="InterruptedReason"/> (the boot-pass
    /// reason); the periodic reaper supplies a distinct reason that names the
    /// repeated-reclaim cause instead of implying a single crash.
    /// </param>
    /// <param name="onReclaimed">
    /// Invoked once per orphan with the reservation age it had before triage
    /// touched it and a short outcome tag, letting a periodic caller log
    /// per-job detail that a one-shot boot pass doesn't need.
    /// </param>
    public static async Task<OrphanTriageResult> RunAsync(
        IQueueContext context,
        IOrphanCheckpointLookup? checkpointLookup,
        IReadOnlyList<QueueJobModel> orphans,
        IReadOnlySet<string> encoderQueues,
        bool refundAttemptOnRequeue,
        Func<QueueJobModel, string>? deadLetterReasonFactory = null,
        Action<QueueJobModel, DateTime?, string>? onReclaimed = null,
        CancellationToken cancellationToken = default
    )
    {
        int failed = 0;
        int requeued = 0;
        int resumable = 0;

        foreach (QueueJobModel orphan in orphans)
        {
            DateTime? reservedAt = orphan.ReservedAt;
            bool isEncoderJob = encoderQueues.Contains(item: orphan.Queue);

            if (isEncoderJob && checkpointLookup is not null)
            {
                bool hasCheckpoint = await checkpointLookup
                    .HasCheckpointAsync(jobPayload: orphan.Payload, ct: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (hasCheckpoint)
                {
                    orphan.Attempts = 0;
                    orphan.ReservedAt = null;
                    context.UpdateJob(job: orphan);
                    resumable++;
                    onReclaimed?.Invoke(arg1: orphan, arg2: reservedAt, arg3: "resumed-from-checkpoint");
                    continue;
                }
            }

            if (orphan.Attempts > 1)
            {
                string reason = deadLetterReasonFactory?.Invoke(arg: orphan) ?? InterruptedReason;

                context.AddFailedJobAndRemoveJob(
                    failedJob: new()
                    {
                        Uuid = Guid.NewGuid(),
                        Connection = "default",
                        Queue = orphan.Queue,
                        Payload = orphan.Payload,
                        Exception = reason,
                        FailedAt = DateTime.UtcNow,
                    },
                    job: orphan
                );
                failed++;
                onReclaimed?.Invoke(arg1: orphan, arg2: reservedAt, arg3: "failed-exhausted");
            }
            else
            {
                if (refundAttemptOnRequeue)
                    orphan.Attempts = (byte)Math.Max(val1: 0, val2: orphan.Attempts - 1);

                orphan.ReservedAt = null;
                context.UpdateJob(job: orphan);
                requeued++;
                onReclaimed?.Invoke(arg1: orphan, arg2: reservedAt, arg3: "requeued");
            }
        }

        context.SaveChanges();

        return new(Failed: failed, Requeued: requeued, Resumable: resumable);
    }
}
