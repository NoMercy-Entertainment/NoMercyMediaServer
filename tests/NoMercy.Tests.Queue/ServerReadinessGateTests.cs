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

using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercyQueue.Readiness;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="ServerReadinessGate"/> holds every queue worker at boot until
/// every registered readiness signal (host-started plus anything a hosted
/// service adds via <see cref="ServerReadinessGate.AddSignal"/>) resolves. A
/// signal registered too late — after the gate sealed its snapshot, or after
/// the gate already fully resolved — must be silently ignored rather than
/// hang every future caller forever. The 300s hard-timeout fallback is real
/// wall-clock and is not exercised here; see the class doc for why it exists.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ServerReadinessGateTests
{
    /// <summary>
    /// Minimal controllable <see cref="IHostApplicationLifetime"/> — the real
    /// ASP.NET Core implementation requires a running host, so the gate's
    /// only external dependency is faked directly.
    /// </summary>
    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public FakeLifetime(bool alreadyStarted = false)
        {
            if (alreadyStarted)
                _started.Cancel();
        }

        public void FireStarted() => _started.Cancel();

        public void StopApplication() { }
    }

    [Fact]
    public async Task WaitForReadyAsync_ResolvesOnceHostStartedSignalCompletes()
    {
        FakeLifetime lifetime = new();
        ServerReadinessGate gate = new(lifetime: lifetime, logger: NullLogger<ServerReadinessGate>.Instance);

        Task waiter = gate.WaitForReadyAsync(ct: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);
        waiter.IsCompleted.Should().BeFalse(because: "no signal has resolved yet");

        lifetime.FireStarted();

        await waiter.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        waiter.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task AddSignal_BeforeSeal_IsIncludedAndGateWaitsForIt()
    {
        FakeLifetime lifetime = new();
        ServerReadinessGate gate = new(lifetime: lifetime, logger: NullLogger<ServerReadinessGate>.Instance);
        TaskCompletionSource extraSignal = new();
        gate.AddSignal(name: "extra", signal: extraSignal.Task);

        lifetime.FireStarted();
        Task waiter = gate.WaitForReadyAsync(ct: CancellationToken.None);

        await Task.Delay(millisecondsDelay: 100);
        waiter
            .IsCompleted.Should()
            .BeFalse(because: "the extra signal registered before seal must block resolution");

        extraSignal.SetResult();

        await waiter.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        waiter.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task AddSignal_AfterGateSealed_IsIgnored_DoesNotBlockResolution()
    {
        FakeLifetime lifetime = new();
        ServerReadinessGate gate = new(lifetime: lifetime, logger: NullLogger<ServerReadinessGate>.Instance);

        lifetime.FireStarted();
        // Seal fires one async tick after ApplicationStarted (Task.Run in the
        // ctor's callback) — give it a moment to actually seal before the late
        // AddSignal, otherwise this would race the ctor's own registration
        // window instead of testing the "too late" path.
        await Task.Delay(millisecondsDelay: 150);

        TaskCompletionSource neverResolves = new();
        gate.AddSignal(name: "too-late", signal: neverResolves.Task);

        Task waiter = gate.WaitForReadyAsync(ct: CancellationToken.None);

        await waiter.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        waiter
            .IsCompletedSuccessfully.Should()
            .BeTrue(because: "a signal added after sealing must be ignored, not awaited");
    }

    [Fact]
    public async Task AddSignal_AfterGateFullyResolved_IsIgnored()
    {
        FakeLifetime lifetime = new();
        ServerReadinessGate gate = new(lifetime: lifetime, logger: NullLogger<ServerReadinessGate>.Instance);

        lifetime.FireStarted();
        await gate.WaitForReadyAsync(ct: CancellationToken.None).WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));

        // Gate is now fully resolved. A signal added at this point must be a
        // silent no-op — no exception, and it must not reopen resolution for
        // a second waiter.
        TaskCompletionSource lateSignal = new();
        Action act = () => gate.AddSignal(name: "very-late", signal: lateSignal.Task);
        act.Should().NotThrow();

        Task secondWaiter = gate.WaitForReadyAsync(ct: CancellationToken.None);
        await secondWaiter.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        secondWaiter.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleWaiters_AllUnblockTogetherOnceSignalsComplete()
    {
        FakeLifetime lifetime = new();
        ServerReadinessGate gate = new(lifetime: lifetime, logger: NullLogger<ServerReadinessGate>.Instance);
        TaskCompletionSource extraSignal = new();
        gate.AddSignal(name: "extra", signal: extraSignal.Task);
        lifetime.FireStarted();

        Task waiterA = gate.WaitForReadyAsync(ct: CancellationToken.None);
        Task waiterB = gate.WaitForReadyAsync(ct: CancellationToken.None);

        await Task.Delay(millisecondsDelay: 100);
        waiterA.IsCompleted.Should().BeFalse();
        waiterB.IsCompleted.Should().BeFalse();

        extraSignal.SetResult();

        await Task.WhenAll(tasks: [waiterA, waiterB]).WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        waiterA.IsCompletedSuccessfully.Should().BeTrue();
        waiterB.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Construction_WhenApplicationAlreadyStarted_SealsImmediately()
    {
        // Simulates a re-entrant construction path (e.g. a test host that has
        // already fired ApplicationStarted before the gate is built) — the
        // ctor's IsCancellationRequested branch must seal on the spot rather
        // than wait for an ApplicationStarted event that already happened.
        FakeLifetime lifetime = new(alreadyStarted: true);
        ServerReadinessGate gate = new(lifetime: lifetime, logger: NullLogger<ServerReadinessGate>.Instance);

        Task waiter = gate.WaitForReadyAsync(ct: CancellationToken.None);

        await waiter.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));
        waiter.IsCompletedSuccessfully.Should().BeTrue();
    }
}
