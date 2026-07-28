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

// A set UserId turns this into a one-person notification, delivered by the
// transport that can currently reach them. Absent, it stays the channel-wide
// push every subscriber of the channel receives.
public record PushDispatchRequest(
    string Channel,
    PushPayload Payload,
    string AccessToken,
    string? Audience = null,
    Guid? UserId = null,
    string? Hub = null
);
