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

public interface IPushDispatcher
{
    // audience is optional and carries straight through to
    // IPushRelayClient.DispatchAsync: absent means every one of this
    // server's subscribers on the channel, present narrows delivery to one
    // person's devices.
    Task DispatchAsync(
        string channel,
        PushPayload payload,
        string accessToken,
        string? audience = null,
        CancellationToken cancellationToken = default
    );
}
