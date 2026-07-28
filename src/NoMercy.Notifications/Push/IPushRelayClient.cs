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

public interface IPushRelayClient
{
    // audience is optional and MUST be omitted from the wire body rather than
    // sent as "" when the caller has no ref: the relay treats an empty
    // audience as "narrow to nobody" and drops every entry, which is not the
    // same thing as the unfiltered broadcast that a null audience means.
    Task DispatchAsync(
        string channel,
        IReadOnlyList<PushRelayEntry> entries,
        string accessToken,
        string? audience = null,
        CancellationToken cancellationToken = default
    );
}
