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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Startup;

using NoMercy.NmSystem.Lifecycle;
namespace NoMercy.Tests.Encoder.Startup;

/// <summary>
/// Background service that runs the FFmpeg capability probe after the host
/// finishes starting. The defer-past-startup contract is in memory as a
/// project rule — startup never blocks on probes — and this background
/// service is the enforcement. Probe failures must be logged and swallowed
/// so a missing dependency can never prevent the server from serving.
/// </summary>
public class FfmpegCapabilityProbeBackgroundServiceTests
{
    private static IServerPhaseTracker CompletedPhaseTracker()
    {
        Mock<IServerPhaseTracker> tracker = new();
        tracker
            .Setup(expression: t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);
        return tracker.Object;
    }

    [Fact]
    public async Task ExecuteAsync_WaitsForApplicationStarted_BeforeProbing()
    {
        // Until the host signals ApplicationStarted, the probe MUST NOT run.
        TestLifetime lifetime = new();
        TaskCompletionSource probeCalled = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IFfmpegCapabilityProbe> probe = new();
        probe
            .Setup(expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .Returns(valueFunction: () =>
            {
                probeCalled.TrySetResult();
                return Task.CompletedTask;
            });

        FfmpegCapabilityProbeBackgroundService service = new(
            probe: probe.Object,
            lifetime: lifetime,
            logger: NullLogger<FfmpegCapabilityProbeBackgroundService>.Instance,
            phaseTracker: CompletedPhaseTracker(),
            initialGrace: TimeSpan.Zero
        );

        using CancellationTokenSource cts = new();
        await StartExecuteAsync(service: service, ct: cts.Token);

        // Allow the wait-for-start path to register the callback.
        await Task.Delay(millisecondsDelay: 50);
        probe.Verify(
            expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()),
            times: Times.Never,
            failMessage: "probe must not fire before ApplicationStarted"
        );

        lifetime.SignalStarted();
        // Wait deterministically on the probe-call TCS — BackgroundService's
        // StartAsync returns when ExecuteAsync yields (not when it completes),
        // so awaiting StartAsync's task races against the post-start probe.
        await probeCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));

        probe.Verify(expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()), times: Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ProbeThrows_SwallowsException()
    {
        // A failing probe MUST NOT crash the host. Server boot continues;
        // the capabilities endpoint returns probe_pending until next retry.
        TestLifetime lifetime = new();
        lifetime.SignalStarted();
        Mock<IFfmpegCapabilityProbe> probe = new();
        probe
            .Setup(expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "ffmpeg not on PATH"));

        FfmpegCapabilityProbeBackgroundService service = new(
            probe: probe.Object,
            lifetime: lifetime,
            logger: NullLogger<FfmpegCapabilityProbeBackgroundService>.Instance,
            phaseTracker: CompletedPhaseTracker(),
            initialGrace: TimeSpan.Zero
        );

        Func<Task> act = () => StartExecuteAsync(service: service, ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringWaitForStart_ReturnsWithoutProbing()
    {
        // Shutdown before ApplicationStarted: probe never runs, no log spam.
        TestLifetime lifetime = new();
        Mock<IFfmpegCapabilityProbe> probe = new();

        FfmpegCapabilityProbeBackgroundService service = new(
            probe: probe.Object,
            lifetime: lifetime,
            logger: NullLogger<FfmpegCapabilityProbeBackgroundService>.Instance,
            phaseTracker: CompletedPhaseTracker(),
            initialGrace: TimeSpan.Zero
        );

        using CancellationTokenSource cts = new();
        Task executeTask = StartExecuteAsync(service: service, ct: cts.Token);
        await Task.Delay(millisecondsDelay: 50);

        await cts.CancelAsync();
        // ExecuteAsync should observe the cancellation and return without
        // running the probe — host is shutting down.
        Func<Task> waitForExit = () => executeTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));
        await waitForExit.Should().NotThrowAsync();

        probe.Verify(expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringInitialGrace_ReturnsWithoutProbing()
    {
        // ApplicationStarted has fired but the grace-period delay is in
        // flight. Cancelling here must short-circuit before probing.
        TestLifetime lifetime = new();
        lifetime.SignalStarted();
        Mock<IFfmpegCapabilityProbe> probe = new();

        FfmpegCapabilityProbeBackgroundService service = new(
            probe: probe.Object,
            lifetime: lifetime,
            logger: NullLogger<FfmpegCapabilityProbeBackgroundService>.Instance,
            phaseTracker: CompletedPhaseTracker(),
            initialGrace: TimeSpan.FromSeconds(seconds: 5)
        );

        using CancellationTokenSource cts = new();
        Task executeTask = StartExecuteAsync(service: service, ct: cts.Token);
        await Task.Delay(millisecondsDelay: 20);
        await cts.CancelAsync();

        await executeTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));

        probe.Verify(expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ProbeOperationCancelled_LogsButDoesNotThrow()
    {
        // Probe observes the shutdown token and throws OperationCanceledException.
        // The service must catch this specifically as a shutdown signal,
        // not propagate it as a fault.
        TestLifetime lifetime = new();
        lifetime.SignalStarted();
        Mock<IFfmpegCapabilityProbe> probe = new();
        probe
            .Setup(expression: p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new OperationCanceledException());

        FfmpegCapabilityProbeBackgroundService service = new(
            probe: probe.Object,
            lifetime: lifetime,
            logger: NullLogger<FfmpegCapabilityProbeBackgroundService>.Instance,
            phaseTracker: CompletedPhaseTracker(),
            initialGrace: TimeSpan.Zero
        );

        Func<Task> act = () => StartExecuteAsync(service: service, ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void DefaultGrace_IsFiveSeconds()
    {
        // The five-second grace is the contract with the rest of the system:
        // long enough for the host to finish writing its startup log lines
        // before the probe starts logging, short enough that capabilities
        // are ready by the time the first /capabilities request lands.
        FfmpegCapabilityProbeBackgroundService
            .DefaultInitialGrace.Should()
            .Be(expected: TimeSpan.FromSeconds(seconds: 5));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static Task StartExecuteAsync(BackgroundService service, CancellationToken ct)
    {
        // BackgroundService.ExecuteAsync is protected; StartAsync is the
        // public entry that hooks it up. Use the public surface to mirror
        // what IHost actually does in production.
        return service.StartAsync(cancellationToken: ct);
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication() => _stopping.Cancel();

        public void SignalStarted() => _started.Cancel();
    }
}
