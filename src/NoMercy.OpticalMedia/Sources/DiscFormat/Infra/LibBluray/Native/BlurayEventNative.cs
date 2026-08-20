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

using System.Runtime.InteropServices;

namespace NoMercy.DiscFormat.LibBluray.Native;

/// Blittable mirror of BD_EVENT (bluray.h).
[StructLayout(LayoutKind.Sequential)]
internal struct BlurayEventNative
{
    public uint Event;
    public uint Param;
}
