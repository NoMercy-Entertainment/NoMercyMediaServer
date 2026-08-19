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

using System.Buffers.Binary;
using System.Text;

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// Reads the big-endian primitives the BDAV/BD-ROM on-disc structures (index.bdmv, BDJO, MPLS) are
/// written in. Every multi-byte field in those formats is network byte order, so a single reader
/// keeps each parser free of byte-shuffling.
internal static class BigEndianReader
{
    public static byte U8(ReadOnlySpan<byte> data, int offset)
    {
        return data[offset];
    }

    public static ushort U16(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, sizeof(ushort)));
    }

    public static uint U32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, sizeof(uint)));
    }

    public static string Ascii(ReadOnlySpan<byte> data, int offset, int length)
    {
        return Encoding.ASCII.GetString(data.Slice(offset, length)).TrimEnd('\0').Trim();
    }
}
