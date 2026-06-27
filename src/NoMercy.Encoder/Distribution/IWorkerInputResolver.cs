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
/// Turns a raw signed worker task payload into a dispatch-ready
/// <see cref="EncodeTask"/>: verifies the HMAC envelope, then ensures the
/// task's source is available locally (fetching from the coordinator when the
/// worker has no direct filesystem access). Lifts this resolution logic out of
/// the transport controller so it can be unit-tested and reused.
/// </summary>
public interface IWorkerInputResolver
{
    /// <summary>
    /// Deserializes and verifies <paramref name="payload"/> with
    /// <paramref name="signingKey"/>, then resolves the task's local source.
    /// </summary>
    Task<WorkerInputResolution> ResolveAsync(
        string payload,
        byte[] signingKey,
        CancellationToken ct
    );

    /// <summary>
    /// Releases any cached source fetched for <paramref name="task"/>. Call
    /// once the task has completed (success or failure).
    /// </summary>
    Task ReleaseAsync(EncodeTask task);
}
