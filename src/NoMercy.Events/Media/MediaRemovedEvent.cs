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

namespace NoMercy.Events.Media;

public sealed class MediaRemovedEvent : EventBase
{
    public override string Source => "MediaProcessor";

    public required int MediaId { get; init; }
    public required string MediaType { get; init; }
    public required string Title { get; init; }
    public required Ulid LibraryId { get; init; }
}
