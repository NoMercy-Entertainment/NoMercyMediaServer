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

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Where a job puts input too big to copy into every payload that needs it.
/// </summary>
public interface IQueueJobBlobStore
{
    /// <summary>
    /// Stores <paramref name="data"/> under <paramref name="key"/>, leaving an
    /// existing row alone. The key identifies the data itself (a release id, say),
    /// so a second writer is storing what is already there.
    /// </summary>
    Task WriteAsync(string key, string data);

    /// <summary>
    /// The stored data, or null when the key was never written or has been swept.
    /// </summary>
    Task<string?> ReadAsync(string key);

    /// <summary>
    /// Drops every blob no queued job still references, and answers how many went.
    /// </summary>
    Task<int> SweepUnreferencedAsync();
}
