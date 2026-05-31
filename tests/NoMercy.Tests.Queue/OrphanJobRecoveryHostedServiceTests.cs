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
        services.AddSingleton<IQueueContext>(context);
        ServiceProvider provider = services.BuildServiceProvider();
        OrphanJobRecoveryHostedService service = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrphanJobRecoveryHostedService>.Instance
        );
        return (service, context);
    }

    [Fact]
    public async Task StartAsync_OrphanWithPriorAttempts_MovedToFailedJobs()
    {
        (OrphanJobRecoveryHostedService service, TestQueueContextAdapter context) = BuildService();
        QueueJobModel orphan = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"id\":\"job-1\"}",
            Priority = 5,
            Attempts = 1,
            ReservedAt = DateTime.UtcNow.AddMinutes(-5),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(orphan);

        await service.StartAsync(CancellationToken.None);

        Assert.Empty(context.Jobs);
        Assert.Single(context.FailedJobs);
        FailedJobModel failed = context.FailedJobs[0];
        Assert.Equal(EncoderQueue, failed.Queue);
        Assert.Equal(orphan.Payload, failed.Payload);
        Assert.Equal("job.interrupted_no_checkpoint", failed.Exception);
        Assert.Equal("default", failed.Connection);
        Assert.NotEqual(Guid.Empty, failed.Uuid);
    }

    [Fact]
    public async Task StartAsync_FirstTimeOrphan_LeftForRetry()
    {
        (OrphanJobRecoveryHostedService service, TestQueueContextAdapter context) = BuildService();
        QueueJobModel orphan = new()
        {
            Queue = EncoderQueue,
            Payload = "{\"id\":\"job-2\"}",
            Priority = 5,
            Attempts = 0,
            ReservedAt = DateTime.UtcNow.AddMinutes(-2),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(orphan);

        await service.StartAsync(CancellationToken.None);

        Assert.Single(context.Jobs);
        Assert.Empty(context.FailedJobs);
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
            ReservedAt = DateTime.UtcNow.AddSeconds(-5),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(reserved);

        await service.StartAsync(CancellationToken.None);

        Assert.Single(context.Jobs);
        Assert.Empty(context.FailedJobs);
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
            AvailableAt = DateTime.UtcNow.AddHours(-1),
        };
        context.AddJob(pending);

        await service.StartAsync(CancellationToken.None);

        Assert.Single(context.Jobs);
        Assert.Empty(context.FailedJobs);
    }
}
