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

namespace NoMercy.Events.Music;

public sealed class MusicItemLikedEvent : EventBase
{
    public override string Source => "Music";

    public required Guid UserId { get; init; }
    public required Guid ItemId { get; init; }
    public required string ItemType { get; init; }
    public required bool Liked { get; init; }
}
