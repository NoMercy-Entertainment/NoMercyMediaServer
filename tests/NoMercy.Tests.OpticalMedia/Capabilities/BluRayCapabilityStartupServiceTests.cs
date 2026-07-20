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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.OpticalMedia.Capabilities;

namespace NoMercy.Tests.OpticalMedia.Capabilities;

/// <summary>
/// REQUIREMENT: <see cref="BluRayCapabilityStartupService"/> must never block
/// or fail application startup on the Blu-ray probe — it waits for
/// <see cref="IHostApplicationLifetime.ApplicationStarted"/>, then for
/// <see cref="BootStage.All"/>, then a grace period, before running the
/// probe. Cancellation at any wait point must return cleanly without
/// probing, and a probe failure (including cooperative cancellation) must be
/// swallowed rather than propagated.
/// </summary>
[Trait("Category", "Unit")]
public class BluRayCapabilityStartupServiceTests
{
    private static IServerPhaseTracker CompletedPhaseTracker()
    {
        Mock<IServerPhaseTracker> tracker = new();
        tracker
            .Setup(t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return tracker.Object;
    }

    private static FfmpegBluRayCapability MakeCapability(Mock<IProcessRunner> runner)
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        return new(options, runner.Object, NullLogger<FfmpegBluRayCapability>.Instance);
    }

    private static Mock<IProcessRunner> MakeSucceedingRunner()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "bluray", "", TimeSpan.Zero));
        return runner;
    }

    private static Task StartExecuteAsync(BackgroundService service, CancellationToken ct) =>
        service.StartAsync(ct);

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeApplicationStarted_NeverProbes()
    {
        TestLifetime lifetime = new();
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayCapabilityStartupService service = new(
            MakeCapability(runner),
            lifetime,
            NullLogger<BluRayCapabilityStartupService>.Instance,
            CompletedPhaseTracker()
        );

        using CancellationTokenSource cts = new();
        Task executeTask = StartExecuteAsync(service, cts.Token);
        await Task.Delay(30);
        await cts.CancelAsync();

        await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

        runner.Verify(
            r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringPhaseWait_NeverProbes()
    {
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        // Phase tracker never resolves until cancellation fires.
        Mock<IServerPhaseTracker> tracker = new();
        TaskCompletionSource waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        tracker
            .Setup(t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns<BootStage, CancellationToken>(
                (_, ct) =>
                {
                    waitStarted.TrySetResult();
                    return Task.Delay(Timeout.Infinite, ct);
                }
            );

        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayCapabilityStartupService service = new(
            MakeCapability(runner),
            lifetime,
            NullLogger<BluRayCapabilityStartupService>.Instance,
            tracker.Object
        );

        using CancellationTokenSource cts = new();
        Task executeTask = StartExecuteAsync(service, cts.Token);
        await waitStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

        runner.Verify(
            r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationAlreadyStarted_SkipsWaitAndReachesPhaseTracker()
    {
        // ApplicationStarted.IsCancellationRequested is already true at
        // construction — the wait-for-start block must be skipped entirely.
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource phaseReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IServerPhaseTracker> tracker = new();
        tracker
            .Setup(t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns<BootStage, CancellationToken>(
                (_, _) =>
                {
                    phaseReached.TrySetResult();
                    return Task.CompletedTask;
                }
            );

        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayCapabilityStartupService service = new(
            MakeCapability(runner),
            lifetime,
            NullLogger<BluRayCapabilityStartupService>.Instance,
            tracker.Object
        );

        using CancellationTokenSource cts = new();
        _ = StartExecuteAsync(service, cts.Token);

        await phaseReached.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ExecuteAsync_ProbeThrows_DoesNotCrashHost()
    {
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource probeCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                (_, _, _, _) =>
                {
                    probeCalled.TrySetResult();
                    throw new InvalidOperationException("ffmpeg missing");
                }
            );

        BluRayCapabilityStartupService service = new(
            MakeCapability(runner),
            lifetime,
            NullLogger<BluRayCapabilityStartupService>.Instance,
            CompletedPhaseTracker()
        );

        // Real 5-second grace applies here (no injectable override) — this is
        // the single test in the suite that pays that cost, to prove the
        // probe path itself (not just the wait gates) swallows failures.
        // BackgroundService.StartAsync returns once ExecuteAsync yields (not
        // when it completes), so the assertion waits on the probe-call TCS
        // rather than on StartAsync's returned task.
        Exception? startupException = await Record.ExceptionAsync(() =>
            StartExecuteAsync(service, CancellationToken.None)
        );
        startupException
            .Should()
            .BeNull("BackgroundService.StartAsync must not surface probe failures");

        await probeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        runner.Verify(
            r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_ProbeThrowsOperationCanceled_LogsInformation_DoesNotCrashHost()
    {
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource probeCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                (_, _, _, _) =>
                {
                    probeCalled.TrySetResult();
                    throw new OperationCanceledException("shutting down");
                }
            );

        BluRayCapabilityStartupService service = new(
            MakeCapability(runner),
            lifetime,
            NullLogger<BluRayCapabilityStartupService>.Instance,
            CompletedPhaseTracker()
        );

        // Same real 5-second grace as the sibling test above — this proves
        // the OperationCanceledException-specific catch clause (distinct
        // from the generic Exception catch) also swallows cleanly.
        Exception? startupException = await Record.ExceptionAsync(() =>
            StartExecuteAsync(service, CancellationToken.None)
        );
        startupException.Should().BeNull();

        await probeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ExecuteAsync_ProbeSucceeds_CompletesTryBlockNormally()
    {
        // Sibling to the two probe-failure tests above: this is the only
        // scenario where ProbeAsync returns without throwing, so the try
        // block's closing brace is reached and control falls past both
        // catch clauses.
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource probeCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                (_, _, _, _) =>
                {
                    probeCalled.TrySetResult();
                    return Task.FromResult(new ProcessResult(0, "bluray", "", TimeSpan.Zero));
                }
            );

        BluRayCapabilityStartupService service = new(
            MakeCapability(runner),
            lifetime,
            NullLogger<BluRayCapabilityStartupService>.Instance,
            CompletedPhaseTracker()
        );

        Exception? startupException = await Record.ExceptionAsync(() =>
            StartExecuteAsync(service, CancellationToken.None)
        );
        startupException.Should().BeNull();

        await probeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
