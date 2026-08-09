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
using NoMercyQueue;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Reclaims stuck queue-job reservations while the server keeps running.
///
/// <see cref="OrphanJobRecoveryHostedService"/> only ever runs once, on boot —
/// a job that starts running and then hangs mid-flight (a stalled TMDB call,
/// a wedged NFS write) holds its <c>ReservedAt</c> forever because nothing
/// ever re-scans for it again. On a long-running server this permanently
/// consumes one of that queue's worker slots per hang, until eventually every
/// slot is stuck and the whole queue stalls — this is exactly what happened to
/// the <c>extras</c> queue (four RESERVED jobs held every worker for 7.5+
/// hours while 1,700+ backlogged jobs never ran).
///
/// <para>Safety design: unlike the boot pass, a periodic pass runs while jobs
/// might genuinely still be executing, so a wall-clock cutoff is only safe for
/// queues KNOWN to finish in seconds-to-minutes. This is therefore an
/// ALLOW-LIST (<see cref="AllowedQueues"/>), not "everything except encoder":</para>
/// <list type="bullet">
/// <item><description>Encoder queues (encoder/encoder-gpu/encoder-cpu)
/// legitimately hold a reservation for hours. Excluded entirely — they stay
/// owned by the boot pass plus their checkpoint-resume path
/// (<see cref="IOrphanCheckpointLookup"/>). The one exception is
/// <c>MusicEncodeJob</c>: it runs on <c>encoder-cpu</c>, the same lane
/// <c>EncodeTaskJob</c>'s real ffmpeg work uses (moved off the plain
/// <c>encoder</c> queue, which used to stall every <c>VideoEncodeJob</c>
/// coordination step behind a music backlog — see its <c>QueueName</c> doc
/// comment), but a single track is bounded like <c>Image</c>/<c>File</c>, not
/// hours-long like a video encode. It has no <c>OutputDirectory</c> for the
/// checkpoint-resume path either, so a wedged reservation was invisible to
/// every recovery path until the next full server restart — see
/// <c>IsReclaimable</c>.</description></item>
/// <item><description><c>library</c> and <c>import</c> also excluded:
/// <c>LibraryScanJob</c>/<c>ShowImportJob</c> can legitimately run well past
/// this reaper's cutoff (a first-time import of a large collection makes
/// synchronous, rate-limited TMDB calls per folder/show). Worse,
/// <c>ReserveJob</c>'s reservation predicate only checks <c>ReservedAt ==
/// null</c> — reclaiming a job that is still actually running lets a SECOND
/// worker pick up the same row and run a concurrent duplicate scan, doubling
/// load on an already-slow NFS/TMDB path. A wall-clock timer cannot tell a
/// legitimately-long scan from a hang, so these queues are left to the boot
/// pass only (a mid-session hang waits for the next restart — rare and
/// acceptable, versus a duplicate whole-library scan).</description></item>
/// </list>
///
/// <para>Reuses <see cref="OrphanRecoveryTriage"/> — the same triage method the
/// boot pass calls — for the requeue/dead-letter decision, but with
/// <c>refundAttemptOnRequeue: false</c>: the boot pass's "refund the attempt
/// for one free clean retry" behavior would otherwise hold a repeatedly
/// hanging job at <c>Attempts == 1</c> forever (never crossing the
/// dead-letter threshold), re-duplicating its work every interval without
/// bound. Not refunding means the next reservation's normal increment carries
/// a repeat-hanging job over that threshold within a couple of reclaims.</para>
/// </summary>
public sealed class StuckReservationReaperHostedService : BackgroundService
{
    /// <summary>How often the reaper scans for stuck reservations.</summary>
    internal static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How long an allow-listed queue's job may sit reserved before it is
    /// treated as stuck. These queues finish in seconds-to-minutes, so this is
    /// generous headroom, not a tight timeout.
    /// </summary>
    internal static readonly TimeSpan DefaultCutoff = TimeSpan.FromMinutes(20);

    /// <summary>
    /// The only queues the periodic reaper ever reclaims. Deliberately an
    /// allow-list, not "everything except encoder" — see the class doc for why
    /// <c>library</c>/<c>import</c> must stay excluded alongside the encoder
    /// queues. <c>file</c> (FileRescanJob) is per-item bounded, so it is safe
    /// to include.
    /// </summary>
    internal static readonly IReadOnlySet<string> AllowedQueues = new HashSet<string>
    {
        QueueNames.Extras,
        QueueNames.Image,
        QueueNames.Palette,
        QueueNames.File,
        QueueNames.Music,
        QueueNames.Cron,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StuckReservationReaperHostedService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _cutoff;

    public StuckReservationReaperHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<StuckReservationReaperHostedService> logger
    )
        : this(scopeFactory, logger, DefaultInterval, DefaultCutoff) { }

    internal StuckReservationReaperHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<StuckReservationReaperHostedService> logger,
        TimeSpan interval,
        TimeSpan cutoff
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = interval;
        _cutoff = cutoff;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await ReapOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a single reap pass. Internal (not private) so tests can invoke it
    /// directly instead of waiting on <see cref="DefaultInterval"/>.
    /// </summary>
    internal async Task ReapOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            SweepStrandedJobs();

            using IServiceScope scope = _scopeFactory.CreateScope();
            IQueueContext context = scope.ServiceProvider.GetRequiredService<IQueueContext>();

            DateTime cutoffUtc = DateTime.UtcNow.Subtract(_cutoff);
            IReadOnlyList<QueueJobModel> reserved = context.GetReservedJobsOlderThan(cutoffUtc);

            List<QueueJobModel> candidates = reserved.Where(IsReclaimable).ToList();

            if (candidates.Count == 0)
                return;

            OrphanTriageResult result = await OrphanRecoveryTriage
                .RunAsync(
                    context,
                    checkpointLookup: null,
                    candidates,
                    encoderQueues: Array.Empty<string>().ToHashSet(),
                    // The host is up and the worker is alive, so a reservation
                    // this old means the JOB is wedged. That is a real failure
                    // and it keeps its attempt — see the class doc's
                    // convergence rationale.
                    interrupted: false,
                    deadLetterReasonFactory: BuildDeadLetterReason,
                    onReclaimed: LogReclaim,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Stuck-reservation reaper: reclaimed {Total} job(s) reserved longer than {Cutoff}; {Failed} moved to FailedJobs, {Requeued} released for retry",
                candidates.Count,
                _cutoff,
                result.Failed,
                result.Requeued
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stuck-reservation reaper pass failed; will retry next interval");
        }
    }

    /// <summary>
    /// Clears rows that are unreserved and out of attempts. Unlike the
    /// reservation reclaim below this needs no allow-list and no cutoff: an
    /// unreserved row is provably not executing, and one at <c>maxAttempts</c>
    /// can never be reserved again, so removing it cannot race a running job on
    /// any queue — the encoder queues included.
    /// <para>Runs on the queue runner's own <see cref="JobQueue"/> rather than a
    /// scoped one: that instance owns the write lock every other queue mutation
    /// takes, and a second instance over a second context would delete rows
    /// outside it.</para>
    /// </summary>
    private void SweepStrandedJobs()
    {
        JobQueue? queue = QueueRunner.Current?.Queue;
        if (queue is null)
            return;

        int stranded = queue.FailStrandedJobs();
        if (stranded > 0)
        {
            _logger.LogWarning(
                "Stuck-reservation reaper: dead-lettered {Stranded} stranded job(s) that had used every attempt without holding a reservation",
                stranded
            );
        }
    }

    /// <summary>
    /// True for anything on <see cref="AllowedQueues"/>, plus the one
    /// deliberate carve-out: a <c>MusicEncodeJob</c> reserved on
    /// <c>encoder-cpu</c>. That queue is otherwise excluded because
    /// <c>EncodeTaskJob</c> legitimately holds it for hours — but a music
    /// track is bounded like the other allow-listed queues, so it must not
    /// inherit the video jobs' exclusion.
    /// </summary>
    private static bool IsReclaimable(QueueJobModel job) =>
        AllowedQueues.Contains(job.Queue)
        || (
            job.Queue == QueueNames.EncoderCpu
            && JobPayloadTypeReader.ReadShortTypeName(job.Payload) == "MusicEncodeJob"
        );

    private static string BuildDeadLetterReason(QueueJobModel job) =>
        $"job.reclaimed_stuck_repeatedly (periodic reaper, attempts={job.Attempts})";

    private void LogReclaim(QueueJobModel job, DateTime? reservedAt, string outcome)
    {
        TimeSpan reservedFor = reservedAt.HasValue
            ? DateTime.UtcNow - reservedAt.Value
            : TimeSpan.Zero;
        string jobType = JobPayloadTypeReader.ReadShortTypeName(job.Payload);

        _logger.LogWarning(
            "Stuck-reservation reaper: reclaimed job {JobId} ({JobType}) on queue {Queue} — reserved for {ReservedFor:g}, outcome: {Outcome}",
            job.Id,
            jobType,
            job.Queue,
            reservedFor,
            outcome
        );
    }
}
