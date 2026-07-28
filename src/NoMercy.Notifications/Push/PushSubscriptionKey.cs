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

// UserRef and UserId are both optional and trail every other member so every
// shorter call site that predates them (tests included) keeps compiling and
// behaving the same: a key with no UserId simply cannot be matched to a
// local user, and a key with no UserRef cannot be dispatched to alone.
public record PushSubscriptionKey(
    long Id,
    string P256dh,
    string Auth,
    string? UserRef = null,
    Guid? UserId = null
);
