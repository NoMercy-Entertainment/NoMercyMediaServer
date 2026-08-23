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

/// The BD_EVENT codes the drive loop reacts to while capturing a BD-J menu. These values are the
/// EXACT numbers from libbluray's bluray.h BD_EVENT enum — a mismatch silently breaks still handling
/// (a menu that issues STILL_TIME waits forever on LOADING unless the caller acknowledges the real
/// event 26 with bd_read_skip_still). Keep this in lockstep with the header.
internal enum BdEvent : uint
{
    None = 0,
    Error = 1,
    ReadError = 2,
    Encrypted = 3,
    Angle = 4,
    Title = 5,
    Playlist = 6,
    Playitem = 7,
    Chapter = 8,
    Playmark = 9,
    EndOfTitle = 10,
    PlaylistStop = 22,
    Discontinuity = 23,
    Seek = 24,
    Still = 25,
    StillTime = 26,
    SoundEffect = 27,
    Idle = 28,
    Popup = 29,
    Menu = 30,
    KeyInterestTable = 32,
    UoMaskChanged = 33,
}
