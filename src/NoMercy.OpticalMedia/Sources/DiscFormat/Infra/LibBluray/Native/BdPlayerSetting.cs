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

namespace NoMercy.DiscFormat.LibBluray.Native;

/// <summary>
/// Maps to bd_player_setting in bluray.h (BLURAY_PLAYER_SETTING_* / BLURAY_PLAYER_* values).
/// </summary>
internal enum BdPlayerSetting : uint
{
    AudioLang = 16,
    PgLang = 17,
    MenuLang = 18,
    CountryCode = 19,
    RegionCode = 20,
    OutputPrefer = 21,
    Parental = 13,
    AudioCap = 15,
    VideoCap = 29,
    DisplayCap = 23,
    PlayerProfile = 31,
    DecodePg = 0x100,
    PersistentStorage = 0x101,
    PersistentRoot = 0x200,
    CacheRoot = 0x201,
    JavaHome = 0x202,
}
