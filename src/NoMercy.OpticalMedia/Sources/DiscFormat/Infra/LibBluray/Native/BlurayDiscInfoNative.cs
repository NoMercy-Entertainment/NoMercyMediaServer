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

/// <summary>
/// Blittable mirror of BLURAY_DISC_INFO (bluray.h) for 64-bit Windows.
/// Uses explicit layout so field offsets survive compiler-alignment differences.
/// Only the fields consumed by the managed wrapper are named; the rest are padding.
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal struct BlurayDiscInfoNative
{
    // offset 0 — uint8_t bluray_detected
    [FieldOffset(0)]
    public byte BlurayDetected;

    // offsets 1..7 — padding (pointer alignment)

    // offset 8 — const char* disc_name
    [FieldOffset(8)]
    public nint DiscNamePtr;

    // offset 16 — const char* udf_volume_id
    [FieldOffset(16)]
    public nint UdfVolumeIdPtr;

    // offsets 24..43 — uint8_t disc_id[20]
    [FieldOffset(24)]
    public unsafe fixed byte DiscId[20];

    // offset 44 — uint8_t no_menu_support
    [FieldOffset(44)]
    public byte NoMenuSupport;

    // offset 45 — uint8_t first_play_supported
    [FieldOffset(45)]
    public byte FirstPlaySupported;

    // offset 46 — uint8_t top_menu_supported
    [FieldOffset(46)]
    public byte TopMenuSupported;

    // offset 47 — padding (align uint32_t)

    // offset 48 — uint32_t num_titles
    [FieldOffset(48)]
    public uint NumTitles;

    // offsets 52..55 — padding (align pointer)

    // offset 56 — const BLURAY_TITLE** titles
    [FieldOffset(56)]
    public nint TitlesPtr;

    // offset 64 — const BLURAY_TITLE* first_play
    [FieldOffset(64)]
    public nint FirstPlayPtr;

    // offset 72 — const BLURAY_TITLE* top_menu
    [FieldOffset(72)]
    public nint TopMenuPtr;

    // offset 80 — uint32_t num_hdmv_titles
    [FieldOffset(80)]
    public uint NumHdmvTitles;

    // offset 84 — uint32_t num_bdj_titles
    [FieldOffset(84)]
    public uint NumBdjTitles;

    // offset 88 — uint32_t num_unsupported_titles
    [FieldOffset(88)]
    public uint NumUnsupportedTitles;

    // offset 92 — uint8_t bdj_detected
    [FieldOffset(92)]
    public byte BdjDetected;

    // offset 93 — uint8_t bdj_supported (deprecated)
    [FieldOffset(93)]
    public byte BdjSupported;

    // offset 94 — uint8_t libjvm_detected
    [FieldOffset(94)]
    public byte LibjvmDetected;

    // offset 95 — uint8_t bdj_handled
    [FieldOffset(95)]
    public byte BdjHandled;

    // offsets 96..104 — char bdj_org_id[9]
    [FieldOffset(96)]
    public unsafe fixed byte BdjOrgId[9];

    // offsets 105..137 — char bdj_disc_id[33]
    [FieldOffset(105)]
    public unsafe fixed byte BdjDiscId[33];

    // offset 138 — uint8_t video_format
    [FieldOffset(138)]
    public byte VideoFormat;

    // offset 139 — uint8_t frame_rate
    [FieldOffset(139)]
    public byte FrameRate;

    // offset 140 — uint8_t content_exist_3D
    [FieldOffset(140)]
    public byte ContentExist3D;

    // offset 141 — uint8_t initial_output_mode_preference
    [FieldOffset(141)]
    public byte InitialOutputModePreference;

    // offsets 142..173 — uint8_t provider_data[32]
    [FieldOffset(142)]
    public unsafe fixed byte ProviderData[32];

    // offset 174 — uint8_t aacs_detected
    [FieldOffset(174)]
    public byte AacsDetected;

    // offset 175 — uint8_t libaacs_detected
    [FieldOffset(175)]
    public byte LibaacsDetected;

    // offset 176 — uint8_t aacs_handled
    [FieldOffset(176)]
    public byte AacsHandled;

    // offset 177 — padding (align int to 4)

    // offset 180 — int aacs_error_code
    [FieldOffset(180)]
    public int AacsErrorCode;

    // offset 184 — int aacs_mkbv
    [FieldOffset(184)]
    public int AacsMkbv;

    // offset 188 — uint8_t bdplus_detected
    [FieldOffset(188)]
    public byte BdplusDetected;

    // offset 189 — uint8_t libbdplus_detected
    [FieldOffset(189)]
    public byte LibbdplusDetected;

    // offset 190 — uint8_t bdplus_handled
    [FieldOffset(190)]
    public byte BdplusHandled;

    // offset 191 — uint8_t bdplus_gen
    [FieldOffset(191)]
    public byte BdplusGen;

    // offset 192 — uint32_t bdplus_date
    [FieldOffset(192)]
    public uint BdplusDate;

    // offset 196 — uint8_t initial_dynamic_range_type
    [FieldOffset(196)]
    public byte InitialDynamicRangeType;
}
