// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Notifications.Push;

namespace NoMercy.Tests.Notifications.Push;

/// <summary>
/// Stands in for the real HTTP round trip in <see cref="CachingPushKeyClient"/>
/// tests. Records how many times it was actually invoked so a cache-hit test
/// can assert the inner call was skipped, and defers to a caller-supplied
/// delegate so a test can make a single instance answer differently call to
/// call (fresh data, then a thrown failure, then fresh data again).
/// </summary>
internal sealed class FakePushKeyClient(Func<Task<PushSubscriptionKey[]>> respond) : IPushKeyClient
{
    public int CallCount { get; private set; }

    public Task<PushSubscriptionKey[]> GetKeysAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        CallCount++;
        return respond();
    }
}
