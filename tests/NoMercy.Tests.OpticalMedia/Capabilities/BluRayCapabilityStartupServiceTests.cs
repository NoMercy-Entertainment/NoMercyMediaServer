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
[Trait(name: "Category", value: "Unit")]
public class BluRayCapabilityStartupServiceTests
{
    private static IServerPhaseTracker CompletedPhaseTracker()
    {
        Mock<IServerPhaseTracker> tracker = new();
        tracker
            .Setup(expression: t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);
        return tracker.Object;
    }

    private static FfmpegBluRayCapability MakeCapability(Mock<IProcessRunner> runner)
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        return new(options: options, processRunner: runner.Object, logger: NullLogger<FfmpegBluRayCapability>.Instance);
    }

    private static Mock<IProcessRunner> MakeSucceedingRunner()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "bluray", StdErr: "", Duration: TimeSpan.Zero));
        return runner;
    }

    private static Task StartExecuteAsync(BackgroundService service, CancellationToken ct) =>
        service.StartAsync(cancellationToken: ct);

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeApplicationStarted_NeverProbes()
    {
        TestLifetime lifetime = new();
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayCapabilityStartupService service = new(
            capability: MakeCapability(runner: runner),
            lifetime: lifetime,
            logger: NullLogger<BluRayCapabilityStartupService>.Instance,
            phaseTracker: CompletedPhaseTracker()
        );

        using CancellationTokenSource cts = new();
        Task executeTask = StartExecuteAsync(service: service, ct: cts.Token);
        await Task.Delay(millisecondsDelay: 30);
        await cts.CancelAsync();

        await executeTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringPhaseWait_NeverProbes()
    {
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        // Phase tracker never resolves until cancellation fires.
        Mock<IServerPhaseTracker> tracker = new();
        TaskCompletionSource waitStarted = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        tracker
            .Setup(expression: t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns<BootStage, CancellationToken>(
                valueFunction: (_, ct) =>
                {
                    waitStarted.TrySetResult();
                    return Task.Delay(millisecondsDelay: Timeout.Infinite, cancellationToken: ct);
                }
            );

        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayCapabilityStartupService service = new(
            capability: MakeCapability(runner: runner),
            lifetime: lifetime,
            logger: NullLogger<BluRayCapabilityStartupService>.Instance,
            phaseTracker: tracker.Object
        );

        using CancellationTokenSource cts = new();
        Task executeTask = StartExecuteAsync(service: service, ct: cts.Token);
        await waitStarted.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));
        await cts.CancelAsync();

        await executeTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationAlreadyStarted_SkipsWaitAndReachesPhaseTracker()
    {
        // ApplicationStarted.IsCancellationRequested is already true at
        // construction — the wait-for-start block must be skipped entirely.
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource phaseReached = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IServerPhaseTracker> tracker = new();
        tracker
            .Setup(expression: t => t.WhenReachedAsync(It.IsAny<BootStage>(), It.IsAny<CancellationToken>()))
            .Returns<BootStage, CancellationToken>(
                valueFunction: (_, _) =>
                {
                    phaseReached.TrySetResult();
                    return Task.CompletedTask;
                }
            );

        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayCapabilityStartupService service = new(
            capability: MakeCapability(runner: runner),
            lifetime: lifetime,
            logger: NullLogger<BluRayCapabilityStartupService>.Instance,
            phaseTracker: tracker.Object
        );

        using CancellationTokenSource cts = new();
        _ = StartExecuteAsync(service: service, ct: cts.Token);

        await phaseReached.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));
    }

    [Fact]
    public async Task ExecuteAsync_ProbeThrows_DoesNotCrashHost()
    {
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource probeCalled = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                valueFunction: (_, _, _, _) =>
                {
                    probeCalled.TrySetResult();
                    throw new InvalidOperationException(message: "ffmpeg missing");
                }
            );

        BluRayCapabilityStartupService service = new(
            capability: MakeCapability(runner: runner),
            lifetime: lifetime,
            logger: NullLogger<BluRayCapabilityStartupService>.Instance,
            phaseTracker: CompletedPhaseTracker()
        );

        // Real 5-second grace applies here (no injectable override) — this is
        // the single test in the suite that pays that cost, to prove the
        // probe path itself (not just the wait gates) swallows failures.
        // BackgroundService.StartAsync returns once ExecuteAsync yields (not
        // when it completes), so the assertion waits on the probe-call TCS
        // rather than on StartAsync's returned task.
        Exception? startupException = await Record.ExceptionAsync(testCode: () =>
            StartExecuteAsync(service: service, ct: CancellationToken.None)
        );
        startupException
            .Should()
            .BeNull(because: "BackgroundService.StartAsync must not surface probe failures");

        await probeCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_ProbeThrowsOperationCanceled_LogsInformation_DoesNotCrashHost()
    {
        TestLifetime lifetime = new();
        lifetime.SignalStarted();

        TaskCompletionSource probeCalled = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                valueFunction: (_, _, _, _) =>
                {
                    probeCalled.TrySetResult();
                    throw new OperationCanceledException(message: "shutting down");
                }
            );

        BluRayCapabilityStartupService service = new(
            capability: MakeCapability(runner: runner),
            lifetime: lifetime,
            logger: NullLogger<BluRayCapabilityStartupService>.Instance,
            phaseTracker: CompletedPhaseTracker()
        );

        // Same real 5-second grace as the sibling test above — this proves
        // the OperationCanceledException-specific catch clause (distinct
        // from the generic Exception catch) also swallows cleanly.
        Exception? startupException = await Record.ExceptionAsync(testCode: () =>
            StartExecuteAsync(service: service, ct: CancellationToken.None)
        );
        startupException.Should().BeNull();

        await probeCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));
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

        TaskCompletionSource probeCalled = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                valueFunction: (_, _, _, _) =>
                {
                    probeCalled.TrySetResult();
                    return Task.FromResult(result: new ProcessResult(ExitCode: 0, StdOut: "bluray", StdErr: "", Duration: TimeSpan.Zero));
                }
            );

        BluRayCapabilityStartupService service = new(
            capability: MakeCapability(runner: runner),
            lifetime: lifetime,
            logger: NullLogger<BluRayCapabilityStartupService>.Instance,
            phaseTracker: CompletedPhaseTracker()
        );

        Exception? startupException = await Record.ExceptionAsync(testCode: () =>
            StartExecuteAsync(service: service, ct: CancellationToken.None)
        );
        startupException.Should().BeNull();

        await probeCalled.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));
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
