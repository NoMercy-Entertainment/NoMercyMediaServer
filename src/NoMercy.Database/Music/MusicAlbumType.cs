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

namespace NoMercy.Database.Music;

/// <summary>
/// Which of the script's five layouts a release files under. Order matters where a
/// release qualifies for more than one: the script tests soundtrack, then other, then
/// classical, each overwriting the last, so classical wins.
/// </summary>
public enum MusicAlbumType
{
    Standard,
    Single,
    Soundtrack,
    Other,
    Classical,
}
