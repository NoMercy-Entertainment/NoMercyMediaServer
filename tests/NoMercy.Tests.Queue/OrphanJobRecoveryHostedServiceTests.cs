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

public class OrphanJobRecoveryHostedServiceTests
{
    private const string EncoderQueue = "encoder";

    private static (
        OrphanJobRecoveryHostedService Service,
        TestQueueContextAdapter Context
    ) BuildService()
    {
        TestQueueContextAdapter context = new();
        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(implementationInstance: context);
        ServiceProvider provider = services.BuildServiceProvider();
        OrphanJobRecoveryHostedService service = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<OrphanJobRecoveryHostedService>.Instance
        );
        return (service, context);
    }

    [Fact]
    public async Task StartAsync_OrphanWithRepeatAttempts_MovedToFailedJobs()
    {
        (OrphanJobRecoveryHostedService service, TestQueueContextAdapter context) = BuildService();
        QueueJobModel orphan = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"id\":\"job-1\"}",
            Priority = 5,
            Attempts = 2,
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -5),
            AvailableAt = DateTime.UtcNow.AddHours(value: -1),
        };
        context.AddJob(job: orphan);

        await service.StartAsync(cancellationToken: CancellationToken.None);
        if (service.ExecuteTask is not null)
            await service.ExecuteTask;

        Assert.Empty(collection: context.Jobs);
        Assert.Single(collection: context.FailedJobs);
        FailedJobModel failed = context.FailedJobs[index: 0];
        Assert.Equal(expected: EncoderQueue, actual: failed.Queue);
        Assert.Equal(expected: orphan.Payload, actual: failed.Payload);
        Assert.Equal(expected: "job.interrupted_no_checkpoint", actual: failed.Exception);
        Assert.Equal(expected: "default", actual: failed.Connection);
        Assert.NotEqual(expected: Guid.Empty, actual: failed.Uuid);
    }

    [Fact]
    public async Task StartAsync_FirstTimeOrphan_ReservationClearedAndAttemptRefunded()
    {
        (OrphanJobRecoveryHostedService service, TestQueueContextAdapter context) = BuildService();
        QueueJobModel orphan = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"id\":\"job-2\"}",
            Priority = 5,
            Attempts = 1,
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -2),
            AvailableAt = DateTime.UtcNow.AddHours(value: -1),
        };
        context.AddJob(job: orphan);

        await service.StartAsync(cancellationToken: CancellationToken.None);
        if (service.ExecuteTask is not null)
            await service.ExecuteTask;

        Assert.Single(collection: context.Jobs);
        Assert.Empty(collection: context.FailedJobs);
        QueueJobModel survivor = context.Jobs[index: 0];
        Assert.Null(value: survivor.ReservedAt);
        Assert.Equal(expected: 0, actual: survivor.Attempts);
    }

    [Fact]
    public async Task StartAsync_RecentlyReservedJob_LeftAlone()
    {
        (OrphanJobRecoveryHostedService service, TestQueueContextAdapter context) = BuildService();
        QueueJobModel reserved = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"id\":\"job-3\"}",
            Priority = 5,
            Attempts = 2,
            ReservedAt = DateTime.UtcNow.AddSeconds(value: -5),
            AvailableAt = DateTime.UtcNow.AddHours(value: -1),
        };
        context.AddJob(job: reserved);

        await service.StartAsync(cancellationToken: CancellationToken.None);
        if (service.ExecuteTask is not null)
            await service.ExecuteTask;

        Assert.Single(collection: context.Jobs);
        Assert.Empty(collection: context.FailedJobs);
    }

    [Fact]
    public async Task StartAsync_NoOrphans_DoesNothing()
    {
        (OrphanJobRecoveryHostedService service, TestQueueContextAdapter context) = BuildService();
        QueueJobModel pending = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"id\":\"job-4\"}",
            Priority = 5,
            Attempts = 0,
            ReservedAt = null,
            AvailableAt = DateTime.UtcNow.AddHours(value: -1),
        };
        context.AddJob(job: pending);

        await service.StartAsync(cancellationToken: CancellationToken.None);
        if (service.ExecuteTask is not null)
            await service.ExecuteTask;

        Assert.Single(collection: context.Jobs);
        Assert.Empty(collection: context.FailedJobs);
    }
}
