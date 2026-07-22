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

        Assert.Same(expected: first, actual: second);
    }

    [Fact]
    public void Shared_SetsCurrentOnFirstCall()
    {
        Assert.Null(@object: ServerPhaseTracker.Current);

        ServerPhaseTracker tracker = ServerPhaseTracker.Shared();

        Assert.Same(expected: tracker, actual: ServerPhaseTracker.Current);
    }

    [Fact]
    public void Shared_PreservesCompletedStagesAcrossContainerRebuild()
    {
        // First "host" resolves the tracker and marks the stages its boot code owns.
        ServiceProvider firstHost = new ServiceCollection()
            .AddSingleton<IServerPhaseTracker>(implementationFactory: _ => ServerPhaseTracker.Shared())
            .BuildServiceProvider();
        IServerPhaseTracker firstTracker = firstHost.GetRequiredService<IServerPhaseTracker>();
        firstTracker.MarkComplete(stage: BootStage.Essential);
        firstTracker.MarkComplete(stage: BootStage.Auth);
        firstHost.Dispose();

        // Second "host" (post-HTTPS restart) builds a fresh container — but the
        // tracker it resolves must already carry the stages the first host marked,
        // otherwise queue workers gated on All would block forever.
        ServiceProvider secondHost = new ServiceCollection()
            .AddSingleton<IServerPhaseTracker>(implementationFactory: _ => ServerPhaseTracker.Shared())
            .BuildServiceProvider();
        IServerPhaseTracker secondTracker = secondHost.GetRequiredService<IServerPhaseTracker>();

        Assert.Same(expected: firstTracker, actual: secondTracker);
        Assert.True(condition: secondTracker.IsComplete(stage: BootStage.Essential));
        Assert.True(condition: secondTracker.IsComplete(stage: BootStage.Auth));

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
        firstTracker.MarkComplete(stage: BootStage.Essential);
        firstTracker.MarkComplete(stage: BootStage.Auth);
        firstTracker.MarkComplete(stage: BootStage.Registered);

        IServerPhaseTracker secondTracker = ServerPhaseTracker.Shared();
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        Task waiter = secondTracker.WhenReachedAsync(stage: BootStage.All, ct: cts.Token);

        Assert.False(condition: waiter.IsCompleted);

        secondTracker.MarkComplete(stage: BootStage.Hardware);
        secondTracker.MarkComplete(stage: BootStage.Binaries);
        secondTracker.MarkComplete(stage: BootStage.Network);

        await waiter;
        Assert.True(condition: secondTracker.IsComplete(stage: BootStage.All));
    }
}
