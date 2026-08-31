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
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// The setup boot swaps the HTTP host for the HTTPS host and calls
/// QueueRunner.StopAll() while most workers are still parked on
/// WhenReachedAsync waiting for a boot stage that host will never reach.
/// That orderly Stop() surfaced as a LogCritical "StartAsync crashed"
/// per worker — 26 red stack traces on every first boot for a shutdown
/// that was working exactly as designed.
/// </summary>
[Trait("Category", "Unit")]
public class QueueWorkerStopDuringPhaseWaitTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueWorkerStopDuringPhaseWaitTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class NeverReachedPhaseTracker : IServerPhaseTracker
    {
        public TaskCompletionSource WaitEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource WaitCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BootStage CompletedStages => BootStage.None;

        public bool IsComplete(BootStage stage) => false;

        public void MarkComplete(BootStage stage) { }

        public event Action<BootStage> StageCompleted = delegate { };

        public async Task WhenReachedAsync(BootStage stage, CancellationToken ct)
        {
            WaitEntered.TrySetResult();
            ct.Register(() => WaitCancelled.TrySetResult());
            _ = StageCompleted;
            await Task.Delay(Timeout.Infinite, ct);
        }
    }

    private sealed class CapturingLogger : ILogger<QueueWorker>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            lock (Entries)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    [Fact]
    public async Task Stop_WhileWaitingForBootStage_IsNotLoggedAsACrash()
    {
        NeverReachedPhaseTracker tracker = new();
        CapturingLogger logger = new();
        QueueWorker worker = new(
            _jobQueue,
            name: "encoder",
            logger: logger,
            phaseTracker: tracker,
            readyStage: BootStage.All
        );

        worker.Start();
        await tracker.WaitEntered.Task.WaitAsync(QueueTestTiming.WaitWindow);

        worker.Stop();
        await tracker.WaitCancelled.Task.WaitAsync(QueueTestTiming.WaitWindow);

        // The wrapper observes the cancellation on a continuation; give it a
        // beat to run before asserting on what it logged.
        await Task.Delay(250);

        lock (logger.Entries)
        {
            logger
                .Entries.Where(e => e.Level >= LogLevel.Error)
                .Should()
                .BeEmpty("an orderly Stop() during the boot-stage wait is not a crash");
        }
    }
}
