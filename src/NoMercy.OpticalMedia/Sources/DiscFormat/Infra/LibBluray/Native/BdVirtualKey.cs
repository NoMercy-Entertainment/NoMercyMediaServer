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
/// BD_VK_* key codes for bd_user_input(). These values are the EXACT numbers from libbluray's
/// keys.h — they are NOT a bit-flag layout (0x01, 0x02...) as a stale version of this enum assumed.
/// A wrong value sends a different key entirely (e.g. "Right"=15, not 4 — 4 is the number key "5"),
/// so navigation silently does the wrong thing and the menu never responds. Keep in lockstep.
/// </summary>
internal enum BdVirtualKey : uint
{
    Key0 = 0,
    Key1 = 1,
    Key2 = 2,
    Key3 = 3,
    Key4 = 4,
    Key5 = 5,
    Key6 = 6,
    Key7 = 7,
    Key8 = 8,
    Key9 = 9,
    RootMenu = 10,
    Popup = 11,
    Up = 12,
    Down = 13,
    Left = 14,
    Right = 15,
    Enter = 16,
    MouseActivate = 17,
    None = 0xffff,
}
