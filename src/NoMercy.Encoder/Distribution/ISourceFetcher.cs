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

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Downloads a task's source file from the coordinator when the worker
/// doesn't have local filesystem access to the path. Returns the local
/// path where the fetched file lives so the encoder pipeline can use it
/// in place of the original <see cref="EncodeTask.InputPath"/>.
///
/// Implementations must:
///   - Verify the coordinator's HMAC signature on the response (the
///     coordinator signs a content hash header so a MITM can't swap
///     bytes mid-transfer).
///   - Stream to disk — source files are routinely multi-GB; loading
///     into memory would OOM the worker.
///   - Be idempotent — if the same task gets retried and the temp file
///     already exists with the expected size, reuse it.
///   - Clean up via <see cref="ReleaseAsync"/> when the task is done
///     so workers don't accumulate tempdirs.
/// </summary>
public interface ISourceFetcher
{
    /// <summary>
    /// Downloads the source for <paramref name="task"/> if not already
    /// present locally and returns the local path to use in the encode
    /// command. When no fetching is needed (local path exists), returns
    /// <see cref="EncodeTask.InputPath"/> unchanged.
    /// </summary>
    Task<string> EnsureLocalAsync(EncodeTask task, CancellationToken ct);

    /// <summary>
    /// Releases any cached source for this task. Call after the task
    /// completes (success or failure). No-op when nothing was fetched.
    /// </summary>
    Task ReleaseAsync(EncodeTask task);
}

/// <summary>
/// Null fetcher that always returns the original input path. Used when
/// coordinator and workers share storage (the common home-server case)
/// and no HTTP fetch is needed.
/// </summary>
public sealed class NullSourceFetcher : ISourceFetcher
{
    public Task<string> EnsureLocalAsync(EncodeTask task, CancellationToken ct) =>
        Task.FromResult(result: task.InputPath ?? string.Empty);

    public Task ReleaseAsync(EncodeTask task) => Task.CompletedTask;
}
