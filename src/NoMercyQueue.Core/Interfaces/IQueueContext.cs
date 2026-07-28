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

using NoMercyQueue.Core.Models;

namespace NoMercyQueue.Core.Interfaces;

public interface IQueueContext : IDisposable
{
    void AddJob(QueueJobModel job);
    void RemoveJob(QueueJobModel job);

    /// <summary>
    /// Reserves the highest-priority job on <paramref name="queueName"/> that is not
    /// already reserved, has not exhausted <paramref name="maxAttempts"/>, and whose
    /// <c>AvailableAt</c> is at or before <paramref name="now"/>. <paramref name="now"/>
    /// is supplied by the caller (rather than read from the database engine's own clock
    /// function) because SQLite's <c>datetime('now')</c> truncates to whole-second
    /// precision while stored <c>AvailableAt</c> values carry sub-second precision —
    /// comparing against a server-side "now" can silently flip the wrong way for a job
    /// available earlier in the same second.
    /// </summary>
    QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId, DateTime now);
    QueueJobModel? FindJob(int id);
    bool JobExists(string payload);
    void UpdateJob(QueueJobModel job);

    /// <summary>
    /// Replaces the payload of an existing queue job and resets its
    /// <c>ReservedAt</c> to null with the given <paramref name="availableAt"/>.
    /// Used by coordinator jobs that need to persist phase state and re-queue
    /// themselves without removing and re-inserting (which would trip the
    /// deduplication check in <see cref="JobQueue.Enqueue"/>).
    /// <para>Attempts resets to zero: a coordinator advancing to its next phase
    /// is new work on the same row, not a retry of failed work. Reservation
    /// increments the counter, so a row that polls in place would otherwise
    /// cross <c>maxAttempts</c> after three wake-ups and quietly stop being
    /// reservable — the encode would hang with no error anywhere.</para>
    /// </summary>
    void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt);

    void ResetAllReservedJobs();
    IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc);

    /// <summary>
    /// Returns the rows <see cref="GetNextJob"/> can never hand out again: not
    /// reserved, so nothing is running them, and already at or past
    /// <paramref name="maxAttempts"/>, so the reserve predicate excludes them.
    /// <para>Such a row is invisible to every other recovery path — the orphan
    /// passes only look at rows that ARE reserved — so without this it stays in
    /// the queue forever, holding whatever position it was dispatched into. A
    /// season that lost one episode to a restart shows that episode at the head
    /// of the queue for the rest of the server's life.</para>
    /// </summary>
    IReadOnlyList<QueueJobModel> GetStrandedJobs(byte maxAttempts);

    /// <summary>
    /// Returns true when the specified parent job ID is present in the
    /// <see cref="FailedJobModel"/> table — indicating that child jobs with this
    /// <paramref name="parentJobId"/> should be skipped (marked as failed-by-parent).
    /// </summary>
    bool IsParentFailed(int parentJobId);

    void AddFailedJob(FailedJobModel failedJob);
    void RemoveFailedJob(FailedJobModel failedJob);
    FailedJobModel? FindFailedJob(int id);
    IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null);

    /// <summary>
    /// Atomically records <paramref name="failedJob"/> and removes <paramref name="job"/>
    /// from the queue as a single unit of work. Dead-letter transitions (FailJob's
    /// max-attempts path and the parent-failed child-skip path) must use this instead of
    /// calling <see cref="AddFailedJob"/> then <see cref="RemoveJob"/> separately — a crash
    /// between two independently-committed calls could otherwise drop the job silently or
    /// record it twice.
    /// </summary>
    void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job);

    IReadOnlyList<CronJobModel> GetEnabledCronJobs();
    CronJobModel? FindCronJobByName(string name);
    void AddCronJob(CronJobModel cronJob);
    void UpdateCronJob(CronJobModel cronJob);
    void RemoveCronJob(CronJobModel cronJob);

    void SaveChanges();
}
