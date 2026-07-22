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

using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// Covers the fail-fast timeout guard used by ShowExtrasJob/EpisodeExtrasJob
/// to bound hang-prone TMDB/NFS store operations. These jobs `new()` their
/// managers directly (no DI), so the operation under test here is the guard
/// itself — a Store call that never completes must throw promptly rather than
/// hang the caller indefinitely.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class JobOperationTimeoutExtensionsTests
{
    [Fact]
    public async Task WithTimeout_OperationNeverCompletes_ThrowsTimeoutExceptionWithinBound()
    {
        TaskCompletionSource neverCompletes = new();

        Func<Task> act = () =>
            neverCompletes.Task.WithTimeout(operationName: "StalledStoreCall", timeout: TimeSpan.FromMilliseconds(milliseconds: 50));

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(testCode: act);
        exception.Message.Should().Contain(expected: "StalledStoreCall");
        exception.Message.Should().Contain(expected: "50");
    }

    [Fact]
    public async Task WithTimeout_OperationCompletesInTime_CompletesNormally()
    {
        Task fastOperation = Task.Delay(millisecondsDelay: 5);

        await fastOperation.WithTimeout(operationName: "FastStoreCall", timeout: TimeSpan.FromSeconds(seconds: 5));

        fastOperation.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WithTimeout_OperationFaults_PropagatesOriginalException()
    {
        Task faulted = Task.FromException(exception: new InvalidOperationException(message: "tmdb 500"));

        Func<Task> act = () => faulted.WithTimeout(operationName: "FaultingStoreCall", timeout: TimeSpan.FromSeconds(seconds: 5));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            testCode: act
        );
        exception.Message.Should().Be(expected: "tmdb 500");
    }

    [Fact]
    public async Task WithTimeout_NoTimeoutSupplied_UsesDefaultTimeoutConstant()
    {
        JobOperationTimeoutExtensions.DefaultTimeout.Should().Be(expected: TimeSpan.FromMinutes(minutes: 3));

        Task fastOperation = Task.Delay(millisecondsDelay: 5);

        await fastOperation.WithTimeout(operationName: "DefaultTimeoutCall");

        fastOperation.IsCompletedSuccessfully.Should().BeTrue();
    }

    /// <summary>
    /// After the guard gives up on a stalled operation, that abandoned task keeps
    /// running and later faults (in the field: its DI-scoped MediaContext is
    /// disposed once the job unwinds, so the next EF call throws
    /// ObjectDisposedException). Nothing awaits it anymore, so without the guard
    /// observing the fault it would resurface as a process-level
    /// UnobservedTaskException from the finalizer thread. Regression guard for
    /// exactly that crash-log spam.
    /// </summary>
    [Fact]
    public async Task WithTimeout_AbandonedOperationFaultsAfterTimeout_DoesNotRaiseUnobserved()
    {
        List<Exception> unobserved = [];

        void Handler(object? _, UnobservedTaskExceptionEventArgs e)
        {
            unobserved.Add(item: e.Exception);
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            await TimeOutThenFaultAbandonedOperationAsync();

            // The abandoned faulted task must be finalized to trigger the
            // unobserved-exception path; scoping its only references inside the
            // helper above lets the GC reclaim it here.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            unobserved.Should().BeEmpty();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }
    }

    private static async Task TimeOutThenFaultAbandonedOperationAsync()
    {
        TaskCompletionSource stalled = new();

        Func<Task> act = () =>
            stalled.Task.WithTimeout(operationName: "AbandonedStore", timeout: TimeSpan.FromMilliseconds(milliseconds: 30));

        await Assert.ThrowsAsync<TimeoutException>(testCode: act);

        // The inner operation faults only AFTER the caller has already unwound —
        // the disposed-MediaContext situation the extras jobs hit in the field.
        stalled.SetException(exception: new InvalidOperationException(message: "MediaContext disposed"));
    }
}
