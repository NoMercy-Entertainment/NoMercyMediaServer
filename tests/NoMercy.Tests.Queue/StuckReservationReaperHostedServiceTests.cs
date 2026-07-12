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
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Queue.MediaServer;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Covers the periodic stuck-reservation reaper — the fix for a job that
/// hangs mid-flight (not just one interrupted by a crash) permanently holding
/// its queue slot, since <see cref="OrphanJobRecoveryHostedService"/> only
/// ever runs once on boot.
///
/// Two safety properties are load-bearing here and both are covered:
/// (1) the reaper only ever touches the allow-listed short-lived queues —
/// encoder AND library/import are excluded because a wall-clock cutoff can't
/// distinguish "still legitimately running" from "hung", and reclaiming a
/// library/import job that's still running would let a second worker start a
/// concurrent duplicate scan; (2) a repeatedly-hanging job on an allow-listed
/// queue converges to FailedJobs instead of looping forever, because the
/// periodic path does not refund the attempt budget the way the boot pass's
/// single-clean-retry does.
/// </summary>
public class StuckReservationReaperHostedServiceTests
{
    private const string ExtrasQueue = "extras";
    private const string ImageQueue = "image";
    private const string LibraryQueue = "library";
    private const string ImportQueue = "import";
    private const string EncoderQueue = "encoder";
    private const string EncoderGpuQueue = "encoder-gpu";

    private static readonly TimeSpan TestCutoff = TimeSpan.FromMinutes(20);

    private static (
        StuckReservationReaperHostedService Service,
        TestQueueContextAdapter Context
    ) BuildService()
    {
        TestQueueContextAdapter context = new();
        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(context);
        ServiceProvider provider = services.BuildServiceProvider();
        StuckReservationReaperHostedService service = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<StuckReservationReaperHostedService>.Instance,
            TimeSpan.FromMinutes(3),
            TestCutoff
        );
        return (service, context);
    }

    // ── Allow-list membership ───────────────────────────────────────────────

    [Fact]
    public async Task ReapOnceAsync_AllowListedQueueReservedLongerThanCutoff_IsReclaimed()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        QueueJobModel stuck = new()
        {
            Queue = ExtrasQueue,
            Payload =
                "{\"$type\":\"NoMercy.MediaProcessing.Jobs.MediaJobs.ShowExtrasJob, NoMercy.MediaProcessing\"}",
            Priority = 1,
            Attempts = 1,
            ReservedAt = DateTime.UtcNow - TestCutoff - TimeSpan.FromMinutes(5),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(stuck);

        await service.ReapOnceAsync(CancellationToken.None);

        QueueJobModel survivor = Assert.Single(context.Jobs);
        Assert.Null(survivor.ReservedAt);
        Assert.Empty(context.FailedJobs);
    }

    [Theory]
    [InlineData(LibraryQueue)]
    [InlineData(ImportQueue)]
    public async Task ReapOnceAsync_LibraryOrImportJobReservedLongerThanCutoff_NotReclaimed(
        string queue
    )
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        // A legitimately long-running whole-library scan / show import,
        // reserved far past the short-lived-queue cutoff but NOT hung.
        QueueJobModel longRunningScan = new()
        {
            Queue = queue,
            Payload = "{\"Id\":\"job-1\"}",
            Priority = 1,
            Attempts = 1,
            ReservedAt = DateTime.UtcNow - TestCutoff - TimeSpan.FromMinutes(30),
            AvailableAt = DateTime.UtcNow.AddHours(-2),
        };
        context.AddJob(longRunningScan);

        await service.ReapOnceAsync(CancellationToken.None);

        QueueJobModel survivor = Assert.Single(context.Jobs);
        Assert.NotNull(survivor.ReservedAt);
        Assert.Equal(1, survivor.Attempts);
        Assert.Empty(context.FailedJobs);
    }

    [Fact]
    public async Task ReapOnceAsync_EncoderJobReservedForHours_NotReclaimed()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        QueueJobModel longRunningEncode = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"OutputDirectory\":\"/media/output\"}",
            Priority = 5,
            Attempts = 1,
            // Reserved far longer than the allow-listed cutoff — a genuine
            // multi-hour encode, not a hang. Must survive the periodic pass.
            ReservedAt = DateTime.UtcNow.AddHours(-6),
            AvailableAt = DateTime.UtcNow.AddHours(-7),
        };
        context.AddJob(longRunningEncode);

        await service.ReapOnceAsync(CancellationToken.None);

        QueueJobModel survivor = Assert.Single(context.Jobs);
        Assert.NotNull(survivor.ReservedAt);
        Assert.Equal(1, survivor.Attempts);
        Assert.Empty(context.FailedJobs);
    }

    [Fact]
    public async Task ReapOnceAsync_EncoderGpuJobReservedForHours_NotReclaimed()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        QueueJobModel longRunningEncode = new()
        {
            Queue = EncoderGpuQueue,
            Payload = "{\"OutputDirectory\":\"/media/output\"}",
            Priority = 5,
            Attempts = 2,
            ReservedAt = DateTime.UtcNow.AddHours(-3),
            AvailableAt = DateTime.UtcNow.AddHours(-4),
        };
        context.AddJob(longRunningEncode);

        await service.ReapOnceAsync(CancellationToken.None);

        Assert.Single(context.Jobs);
        Assert.Empty(context.FailedJobs);
    }

    // ── Attempt-budget convergence (no refund on the periodic path) ────────

    [Fact]
    public async Task ReapOnceAsync_RepeatedlyStuckJob_ConvergesToFailedJobs_NotInfiniteRetry()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        // Simulates a job that has already been reserved once (Attempts=1,
        // set by ReserveJob's own increment) and hung.
        QueueJobModel stuck = new()
        {
            Queue = ExtrasQueue,
            Payload = "{\"Id\":\"job-hangs\"}",
            Priority = 1,
            Attempts = 1,
            ReservedAt = DateTime.UtcNow - TestCutoff - TimeSpan.FromMinutes(1),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(stuck);

        // First reclaim: requeued WITHOUT a refund (Attempts stays 1) —
        // otherwise this job would sit at Attempts=1 forever and never cross
        // the dead-letter threshold.
        await service.ReapOnceAsync(CancellationToken.None);
        QueueJobModel afterFirstReclaim = Assert.Single(context.Jobs);
        Assert.Null(afterFirstReclaim.ReservedAt);
        Assert.Equal(1, afterFirstReclaim.Attempts);
        Assert.Empty(context.FailedJobs);

        // The job gets picked up again (ReserveJob's normal increment) and
        // hangs a second time.
        afterFirstReclaim.Attempts++;
        afterFirstReclaim.ReservedAt = DateTime.UtcNow - TestCutoff - TimeSpan.FromMinutes(1);

        // Second reclaim: Attempts is now 2 (> 1) — dead-letters instead of
        // requeuing again, so the job cannot loop forever.
        await service.ReapOnceAsync(CancellationToken.None);

        Assert.Empty(context.Jobs);
        FailedJobModel failed = Assert.Single(context.FailedJobs);
        Assert.Equal(ExtrasQueue, failed.Queue);
        Assert.Contains("reclaimed_stuck_repeatedly", failed.Exception);

        // A third pass must not re-fail or duplicate — the job is gone.
        await service.ReapOnceAsync(CancellationToken.None);
        Assert.Single(context.FailedJobs);
    }

    [Fact]
    public async Task ReapOnceAsync_JobAlreadyExhausted_DeadLettersOnFirstPeriodicReclaim()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        // Already burned two prior attempts through normal execution
        // failures before this hang — a repeat offender from the first
        // periodic reclaim, same as the boot-pass rule.
        QueueJobModel repeatOffender = new()
        {
            Queue = ImageQueue,
            Payload = "{\"Id\":\"job-2\"}",
            Priority = 1,
            Attempts = 2,
            ReservedAt = DateTime.UtcNow - TestCutoff - TimeSpan.FromMinutes(1),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(repeatOffender);

        await service.ReapOnceAsync(CancellationToken.None);

        Assert.Empty(context.Jobs);
        FailedJobModel failed = Assert.Single(context.FailedJobs);
        Assert.Contains("reclaimed_stuck_repeatedly", failed.Exception);
    }

    [Fact]
    public async Task ReapOnceAsync_RecentlyReservedAllowListedJob_LeftAlone()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        QueueJobModel active = new()
        {
            Queue = ExtrasQueue,
            Payload = "{\"Id\":\"job-3\"}",
            Priority = 1,
            Attempts = 1,
            ReservedAt = DateTime.UtcNow.AddMinutes(-2),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(active);

        await service.ReapOnceAsync(CancellationToken.None);

        QueueJobModel survivor = Assert.Single(context.Jobs);
        Assert.NotNull(survivor.ReservedAt);
        Assert.Empty(context.FailedJobs);
    }

    [Fact]
    public async Task ReapOnceAsync_NoStuckJobs_DoesNothing()
    {
        (StuckReservationReaperHostedService service, TestQueueContextAdapter context) =
            BuildService();

        QueueJobModel pending = new()
        {
            Queue = ExtrasQueue,
            Payload = "{\"Id\":\"job-4\"}",
            Priority = 1,
            Attempts = 0,
            ReservedAt = null,
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(pending);

        await service.ReapOnceAsync(CancellationToken.None);

        Assert.Single(context.Jobs);
        Assert.Empty(context.FailedJobs);
    }
}
