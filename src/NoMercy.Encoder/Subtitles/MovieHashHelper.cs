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

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// OpenSubtitles "moviehash" algorithm.
/// Hash = fileSize + sum of first 64KB as 8-byte LE chunks + sum of last 64KB as 8-byte LE chunks.
/// Files smaller than 64KB overlap — both head and tail read from the same range.
/// </summary>
public static class MovieHashHelper
{
    private const int ChunkSize = 65536;

    public static ulong ComputeMovieHash(Stream stream, long fileSize)
    {
        ulong hash = (ulong)fileSize;

        byte[] head = new byte[ChunkSize];
        stream.Position = 0;
        int headRead = stream.Read(head, 0, ChunkSize);
        hash = AddBuffer(hash, head, headRead);

        byte[] tail = new byte[ChunkSize];
        long tailOffset = Math.Max(0, fileSize - ChunkSize);
        stream.Position = tailOffset;
        int tailRead = stream.Read(tail, 0, ChunkSize);
        hash = AddBuffer(hash, tail, tailRead);

        return hash;
    }

    public static string FormatHash(ulong hash) => hash.ToString("x16");

    private static ulong AddBuffer(ulong h, byte[] buf, int count)
    {
        int paddedCount = count - (count % 8);
        for (int i = 0; i < paddedCount; i += 8)
            h += BitConverter.ToUInt64(buf, i);
        return h;
    }
}
