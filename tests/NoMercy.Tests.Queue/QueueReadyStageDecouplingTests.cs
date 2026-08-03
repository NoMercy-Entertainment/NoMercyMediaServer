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
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Drives the real <see cref="QueueRunner"/> / <see cref="ServerPhaseTracker"/>
/// against the exact stage combination <c>ServiceRegistration.BuildQueueReadyStages</c>
/// assigns the "library" queue — proving the mechanism behind the boot-stage
/// re-cut, not a reimplementation of it. Before the fix every queue defaulted to
/// <see cref="BootStage.All"/> (Essential | Auth | Binaries | Network |
/// Registered), so a library scan waited on <see cref="BootStage.Registered"/>
/// (SSL + cloud registration) even though scanning never touches either. These
/// tests never mark Registered or Hardware at all — if the worker still ran,
/// the readyStage genuinely does not require them.
/// </summary>
[Trait("Category", "Unit")]
public class QueueReadyStageDecouplingTests
{
    private static async Task WaitUntilAsync(Func<bool> predicate, string failureMessage)
    {
        using CancellationTokenSource cts = new(QueueTestTiming.WaitWindow);
        while (!cts.IsCancellationRequested)
        {
            if (predicate())
                return;
            await Task.Delay(10);
        }
        throw new TimeoutException(failureMessage);
    }

    private static QueueRunner BuildLibraryRunner(
        TestQueueContextAdapter context,
        ServerPhaseTracker tracker,
        BootStage libraryReadyStage
    )
    {
        QueueConfiguration configuration = new() { WorkerCounts = new() { ["library"] = 1 } };
        return new(
            context,
            configuration,
            NullLoggerFactory.Instance,
            configurationStore: null,
            scopeFactory: null,
            phaseTracker: tracker,
            resourceBudget: null,
            resourceAwareQueues: null,
            activityGate: null,
            queueReadyStages: new Dictionary<string, BootStage> { ["library"] = libraryReadyStage }
        );
    }

    /// <summary>
    /// The critical regression case from the task: a job on the "library" queue
    /// must run once Essential, Auth, Network and Binaries are complete — the
    /// exact combination <c>BuildQueueReadyStages</c> assigns it — even though
    /// Registered (and Hardware) are never marked at all. Under the old
    /// BootStage.All default this job would sit unreserved forever.
    /// </summary>
    [Fact]
    public async Task LibraryQueue_RunsJob_WithoutRegisteredOrHardwareEverMarked()
    {
        TestQueueContextAdapter context = new();
        ServerPhaseTracker tracker = new();
        BootStage libraryReady =
            BootStage.Essential | BootStage.Auth | BootStage.Network | BootStage.Binaries;
        QueueRunner runner = BuildLibraryRunner(context, tracker, libraryReady);

        TestJob job = new() { Message = "scan while whisper is still downloading" };
        runner.Queue.Enqueue(
            new QueueJobModel
            {
                Queue = "library",
                Payload = SerializationHelper.Serialize(job),
                AvailableAt = DateTime.UtcNow,
            }
        );

        await runner.Initialize();

        // Every stage the library queue actually needs — deliberately excluding
        // Registered and Hardware.
        tracker.MarkComplete(BootStage.Essential);
        tracker.MarkComplete(BootStage.Auth);
        tracker.MarkComplete(BootStage.Network);
        tracker.MarkComplete(BootStage.Binaries);

        await WaitUntilAsync(
            () => context.Jobs.Count == 0,
            "the library worker must reserve and complete its job once Essential/Auth/Network/"
                + "Binaries are marked, without ever needing Registered or Hardware"
        );

        tracker.IsComplete(BootStage.Registered).Should().BeFalse();
        tracker.IsComplete(BootStage.Hardware).Should().BeFalse();

        await runner.StopAll();
    }

    /// <summary>
    /// The library worker must stay idle until Binaries is marked — this models
    /// "ffmpeg/ffprobe are not yet on disk" (whisper/tesseract are irrelevant to
    /// the stage now, but ffmpeg genuinely is). Proves the gate is real, not a
    /// no-op that happens to pass the first test.
    /// </summary>
    [Fact]
    public async Task LibraryQueue_DoesNotRunJob_UntilBinariesMarked()
    {
        TestQueueContextAdapter context = new();
        ServerPhaseTracker tracker = new();
        BootStage libraryReady =
            BootStage.Essential | BootStage.Auth | BootStage.Network | BootStage.Binaries;
        QueueRunner runner = BuildLibraryRunner(context, tracker, libraryReady);

        TestJob job = new() { Message = "must wait for ffmpeg" };
        runner.Queue.Enqueue(
            new QueueJobModel
            {
                Queue = "library",
                Payload = SerializationHelper.Serialize(job),
                AvailableAt = DateTime.UtcNow,
            }
        );

        await runner.Initialize();

        tracker.MarkComplete(BootStage.Essential);
        tracker.MarkComplete(BootStage.Auth);
        tracker.MarkComplete(BootStage.Network);
        // Binaries deliberately left unmarked.

        await Task.Delay(300);
        context.Jobs.Should().ContainSingle(j => j.ReservedAt == null);

        tracker.MarkComplete(BootStage.Binaries);

        await WaitUntilAsync(
            () => context.Jobs.Count == 0,
            "marking Binaries must release the job that was waiting on it"
        );

        await runner.StopAll();
    }
}
