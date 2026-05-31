using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Lifecycle;

namespace NoMercy.Tests.Setup;

// Regression for the host-swap bug: the Service rebuilds its WebApplication on
// HTTPS restart and on port-conflict retry. Before the fix, each container had
// its own ServerPhaseTracker, so MarkComplete calls that routed through the
// static `Current` accessor only ever updated the first host's tracker — the
// new host's queue workers waited on `WhenReachedAsync(All)` forever and the
// queue silently stopped processing jobs.
public class ServerPhaseTrackerSharedTests : IDisposable
{
    public ServerPhaseTrackerSharedTests() => ServerPhaseTracker.ResetSharedForTests();

    public void Dispose() => ServerPhaseTracker.ResetSharedForTests();

    [Fact]
    public void Shared_ReturnsSameInstanceAcrossCalls()
    {
        ServerPhaseTracker first = ServerPhaseTracker.Shared();
        ServerPhaseTracker second = ServerPhaseTracker.Shared();

        Assert.Same(first, second);
    }

    [Fact]
    public void Shared_SetsCurrentOnFirstCall()
    {
        Assert.Null(ServerPhaseTracker.Current);

        ServerPhaseTracker tracker = ServerPhaseTracker.Shared();

        Assert.Same(tracker, ServerPhaseTracker.Current);
    }

    [Fact]
    public void Shared_PreservesCompletedStagesAcrossContainerRebuild()
    {
        // First "host" resolves the tracker and marks the stages its boot code owns.
        ServiceProvider firstHost = new ServiceCollection()
            .AddSingleton<IServerPhaseTracker>(_ => ServerPhaseTracker.Shared())
            .BuildServiceProvider();
        IServerPhaseTracker firstTracker = firstHost.GetRequiredService<IServerPhaseTracker>();
        firstTracker.MarkComplete(BootStage.Essential);
        firstTracker.MarkComplete(BootStage.Auth);
        firstHost.Dispose();

        // Second "host" (post-HTTPS restart) builds a fresh container — but the
        // tracker it resolves must already carry the stages the first host marked,
        // otherwise queue workers gated on All would block forever.
        ServiceProvider secondHost = new ServiceCollection()
            .AddSingleton<IServerPhaseTracker>(_ => ServerPhaseTracker.Shared())
            .BuildServiceProvider();
        IServerPhaseTracker secondTracker = secondHost.GetRequiredService<IServerPhaseTracker>();

        Assert.Same(firstTracker, secondTracker);
        Assert.True(secondTracker.IsComplete(BootStage.Essential));
        Assert.True(secondTracker.IsComplete(BootStage.Auth));

        secondHost.Dispose();
    }

    [Fact]
    public async Task WhenReachedAsync_ResolvesAfterAllStagesMarkedAcrossHosts()
    {
        // Simulates the real boot order: first host marks Essential/Auth/Registered
        // via the static Current, second host spawns workers that wait on All, then
        // late marks (Binaries/Network from Setup.Start, Hardware from the encoder
        // hosted service) finish the bitset.
        IServerPhaseTracker firstTracker = ServerPhaseTracker.Shared();
        firstTracker.MarkComplete(BootStage.Essential);
        firstTracker.MarkComplete(BootStage.Auth);
        firstTracker.MarkComplete(BootStage.Registered);

        IServerPhaseTracker secondTracker = ServerPhaseTracker.Shared();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        Task waiter = secondTracker.WhenReachedAsync(BootStage.All, cts.Token);

        Assert.False(waiter.IsCompleted);

        secondTracker.MarkComplete(BootStage.Hardware);
        secondTracker.MarkComplete(BootStage.Binaries);
        secondTracker.MarkComplete(BootStage.Network);

        await waiter;
        Assert.True(secondTracker.IsComplete(BootStage.All));
    }
}
