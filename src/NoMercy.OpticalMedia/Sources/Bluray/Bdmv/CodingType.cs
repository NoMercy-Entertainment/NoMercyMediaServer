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

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// The full BDAV stream coding-type whitelist, shared by the MPLS STN and CLPI program parsers.
/// Faithful to the libbluray switch in clpi_parse.c:_parse_stream_attr (lines 60-114) plus the
/// public names in bluray.h:bd_stream_type_e. A disc that uses VC-1 or DTS must resolve here,
/// never fall through to "unknown". 0x20 (MVC dependent view) and 0xa0 appear only in the parser
/// switch, not the public enum; 0xa0 keeps its hex name because the reference gives it none.
public static class CodingType
{
    private static readonly Dictionary<int, CodingTypeInfo> Table = new[]
    {
        new CodingTypeInfo
        {
            Value = 0x01,
            Kind = CodingKind.Video,
            Name = "mpeg1-video",
        },
        new CodingTypeInfo
        {
            Value = 0x02,
            Kind = CodingKind.Video,
            Name = "mpeg2-video",
        },
        new CodingTypeInfo
        {
            Value = 0x1B,
            Kind = CodingKind.Video,
            Name = "h264",
        },
        new CodingTypeInfo
        {
            Value = 0x20,
            Kind = CodingKind.Video,
            Name = "h264-mvc",
        },
        new CodingTypeInfo
        {
            Value = 0x24,
            Kind = CodingKind.Video,
            Name = "hevc",
        },
        new CodingTypeInfo
        {
            Value = 0xEA,
            Kind = CodingKind.Video,
            Name = "vc1",
        },
        new CodingTypeInfo
        {
            Value = 0x03,
            Kind = CodingKind.Audio,
            Name = "mpeg1-audio",
        },
        new CodingTypeInfo
        {
            Value = 0x04,
            Kind = CodingKind.Audio,
            Name = "mpeg2-audio",
        },
        new CodingTypeInfo
        {
            Value = 0x80,
            Kind = CodingKind.Audio,
            Name = "lpcm",
        },
        new CodingTypeInfo
        {
            Value = 0x81,
            Kind = CodingKind.Audio,
            Name = "ac3",
        },
        new CodingTypeInfo
        {
            Value = 0x82,
            Kind = CodingKind.Audio,
            Name = "dts",
        },
        new CodingTypeInfo
        {
            Value = 0x83,
            Kind = CodingKind.Audio,
            Name = "truehd",
        },
        new CodingTypeInfo
        {
            Value = 0x84,
            Kind = CodingKind.Audio,
            Name = "eac3",
        },
        new CodingTypeInfo
        {
            Value = 0x85,
            Kind = CodingKind.Audio,
            Name = "dts-hd",
        },
        new CodingTypeInfo
        {
            Value = 0x86,
            Kind = CodingKind.Audio,
            Name = "dts-hd-ma",
        },
        new CodingTypeInfo
        {
            Value = 0xA1,
            Kind = CodingKind.Audio,
            Name = "eac3-secondary",
        },
        new CodingTypeInfo
        {
            Value = 0xA2,
            Kind = CodingKind.Audio,
            Name = "dts-hd-secondary",
        },
        new CodingTypeInfo
        {
            Value = 0x90,
            Kind = CodingKind.Graphics,
            Name = "pg",
        },
        new CodingTypeInfo
        {
            Value = 0x91,
            Kind = CodingKind.Graphics,
            Name = "ig",
        },
        new CodingTypeInfo
        {
            Value = 0xA0,
            Kind = CodingKind.Graphics,
            Name = "graphics-0xa0",
        },
        new CodingTypeInfo
        {
            Value = 0x92,
            Kind = CodingKind.Text,
            Name = "textst",
        },
    }.ToDictionary(info => info.Value);

    public static CodingTypeInfo? Resolve(int codingType)
    {
        return Table.GetValueOrDefault(codingType);
    }

    public static IReadOnlyCollection<CodingTypeInfo> All => Table.Values;
}
