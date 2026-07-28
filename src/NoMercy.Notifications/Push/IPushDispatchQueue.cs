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

namespace NoMercy.Notifications.Push;

public interface IPushDispatchQueue
{
    /// <summary>
    /// Accepts a notification for later delivery and returns immediately.
    /// Never blocks, never throws, and never reaches the network on the
    /// caller's thread.
    /// </summary>
    void Enqueue(PushDispatchRequest request);

    /// <summary>
    /// Delivers queued notifications one at a time until cancelled. Runs on a
    /// single background worker, never on an event publisher.
    /// </summary>
    Task DrainAsync(CancellationToken cancellationToken);
}
