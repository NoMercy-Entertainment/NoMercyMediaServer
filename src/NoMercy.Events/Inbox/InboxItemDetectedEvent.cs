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

namespace NoMercy.Events.Inbox;

public sealed class InboxItemDetectedEvent : EventBase
{
    public override string Source => "Inbox";

    public required string Id { get; init; }
    public required string DetectedType { get; init; }
    public required string Confidence { get; init; }
    public required string Status { get; init; }
}
