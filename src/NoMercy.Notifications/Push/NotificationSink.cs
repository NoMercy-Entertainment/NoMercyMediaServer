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

using NoMercy.Notifications.Transports;

namespace NoMercy.Notifications.Push;

public class NotificationSink(IPushDispatchQueue queue, NotificationDispatcher dispatcher)
{
    public void Notify(string channel, PushPayload payload, string accessToken)
    {
        queue.Enqueue(new(channel, payload, accessToken));
    }

    public Task NotifyUserAsync(
        Guid userId,
        string channel,
        PushPayload payload,
        CancellationToken ct
    )
    {
        return dispatcher.DispatchAsync(new(userId, channel, payload), ct);
    }
}
