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

/// Blittable mirror of BD_OVERLAY (overlay.h).
[StructLayout(LayoutKind.Sequential)]
internal struct BlurayOverlayNative
{
    public long Pts;
    public byte Plane;
    public byte Cmd;
    public byte PaletteUpdateFlag;
    public ushort X;
    public ushort Y;
    public ushort W;
    public ushort H;
    public nint PalettePtr;
    public nint ImgPtr;
}

/// Blittable mirror of BD_PG_PALETTE_ENTRY (overlay.h): one YCrCbT palette slot.
[StructLayout(LayoutKind.Sequential)]
internal struct BlurayPgPaletteEntryNative
{
    public byte Y;
    public byte Cr;
    public byte Cb;
    public byte T;
}

/// bd_overlay_cmd_e (overlay.h).
internal enum BdOverlayCmd : byte
{
    Init = 0,
    Close = 1,
    Clear = 2,
    Draw = 3,
    Wipe = 4,
    Hide = 5,
    Flush = 6,
}

/// Blittable mirror of BD_ARGB_OVERLAY (overlay.h).
[StructLayout(LayoutKind.Sequential)]
internal struct BlurayArgbOverlayNative
{
    public long Pts;
    public byte Plane;
    public byte Cmd;
    public ushort X;
    public ushort Y;
    public ushort W;
    public ushort H;
    public ushort Stride;
    public nint ArgbPtr;
}
