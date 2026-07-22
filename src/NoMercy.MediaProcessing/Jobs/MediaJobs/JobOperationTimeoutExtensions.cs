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

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Bounds a single fetch/store operation so a stalled TMDB call or a wedged
/// NFS write throws instead of hanging the job (and the worker's queue slot)
/// forever. This is defense-in-depth alongside the queue's stuck-reservation
/// reaper — the reaper reclaims the *reservation* on some interval, this makes
/// the *job itself* fail fast so the reaper rarely needs to intervene.
///
/// <para>The wrapped repository/manager methods do not accept a
/// <see cref="CancellationToken"/> (threading one through the whole
/// provider-client + storage stack is a larger change than this fix), so this
/// uses <see cref="Task.WaitAsync(TimeSpan)"/>: the awaiting job observes a
/// <see cref="TimeoutException"/> and unwinds immediately, even though the
/// abandoned inner task keeps running to completion (or its own failure) in
/// the background. That trade-off is acceptable here — the goal is to stop
/// the JOB from wedging a worker slot, not to cancel the underlying I/O.</para>
/// </summary>
public static class JobOperationTimeoutExtensions
{
    /// <summary>
    /// Default per-operation timeout for extras-enrichment calls (TMDB fetch
    /// + image/translation storage). Generous enough for a slow TMDB response
    /// or a busy NFS mount, short enough that one hang no longer holds an
    /// `extras` worker slot for hours.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(minutes: 3);

    public static async Task WithTimeout(
        this Task operation,
        string operationName,
        TimeSpan? timeout = null
    )
    {
        TimeSpan effectiveTimeout = timeout ?? DefaultTimeout;

        try
        {
            await operation.WaitAsync(timeout: effectiveTimeout).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (TimeoutException ex)
        {
            // WaitAsync stops awaiting the operation but cannot cancel it, so the
            // abandoned task keeps running and typically faults later — its
            // DI-scoped MediaContext is disposed the moment this job unwinds, and
            // the next EF call on it throws ObjectDisposedException. Nothing awaits
            // that task anymore, so the fault would otherwise resurface as a
            // process-level UnobservedTaskException on the finalizer thread. Observe
            // it here to keep the abandoned failure contained.
            operation.ObserveExceptionWhenFaulted();
            throw new TimeoutException(message: $"{operationName} timed out after {effectiveTimeout}", innerException: ex);
        }
    }

    /// <summary>
    /// Marks a fire-and-forget task's eventual fault as observed so it never
    /// bubbles up as an <see cref="UnobservedTaskException"/>. A no-op when the
    /// task ultimately completes successfully.
    /// </summary>
    private static void ObserveExceptionWhenFaulted(this Task task)
    {
        _ = task.ContinueWith(
            continuationFunction: static faulted => _ = faulted.Exception,
            cancellationToken: CancellationToken.None,
            continuationOptions: TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            scheduler: TaskScheduler.Default
        );
    }
}
