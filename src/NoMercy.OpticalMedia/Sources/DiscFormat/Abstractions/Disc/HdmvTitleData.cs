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

namespace NoMercy.DiscFormat.Abstractions.Disc;

/// The parsed HDMV menu structures the pipeline hands the transpiler: the raw MovieObject.bdmv bytes
/// and the IG page bytes (one blob per page), already located and extracted from the disc's M2TS by
/// the pipeline. The transpiler decodes and lowers these; it never touches the disc. Empty when the
/// disc carries no HDMV menu (e.g. a BD-J title whose HDMV objects are pure title-routers).
///
/// IgSegmentStream carries the complete IG display set bytes (ICS + ODS + PDS segments contiguous)
/// as extracted from the menu PID of the title's M2TS. When present the assembler uses it to decode
/// ODS graphics objects and emit real sprite PNGs into the bundle. When absent the assembler falls
/// back to IgPageBytes for structural-only output (hotspot placeholders).
public sealed record HdmvTitleData
{
    public byte[] MovieObjectBytes { get; init; } = [];

    public IReadOnlyList<byte[]> IgPageBytes { get; init; } = [];

    public IReadOnlyList<byte[]> PaletteBytes { get; init; } = [];

    public byte[] IgSegmentStream { get; init; } = [];
}
