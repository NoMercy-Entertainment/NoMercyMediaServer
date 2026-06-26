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

namespace NoMercy.Events.Library;

public sealed class LibraryScanCompletedEvent : EventBase
{
    public override string Source => "LibraryScanner";

    public required Ulid LibraryId { get; init; }
    public required string LibraryName { get; init; }
    public required int ItemsFound { get; init; }
    public required TimeSpan Duration { get; init; }
}
