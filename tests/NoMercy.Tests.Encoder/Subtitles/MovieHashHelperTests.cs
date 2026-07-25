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

using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// OpenSubtitles "moviehash" algorithm. Compatible implementations exist on
/// the OS provider side — divergence breaks lookups silently because the
/// query just returns no results. These tests pin the algorithm so changes
/// to chunk size or accumulation can't slip in.
/// </summary>
public class MovieHashHelperTests
{
    private const int ChunkSize = 65536;

    [Fact]
    public void ComputeMovieHash_ZeroBytes_ReturnsFileSize()
    {
        // Empty file: hash = fileSize + 0 + 0 = 0.
        using MemoryStream stream = new();
        ulong hash = MovieHashHelper.ComputeMovieHash(stream, 0);
        hash.Should().Be(0UL);
    }

    [Fact]
    public void ComputeMovieHash_KnownDeterministicInput()
    {
        // File of 256 KB filled with byte 0x01. Each 8-byte LE chunk reads as
        // 0x0101010101010101. ChunkSize=64KB = 8192 chunks contribute to both
        // head and tail (they overlap when file <= ChunkSize, but here file is
        // 256KB so head reads first 64KB and tail reads last 64KB — no overlap).
        const int fileSize = 256 * 1024;
        byte[] data = new byte[fileSize];
        Array.Fill(data, (byte)0x01);
        using MemoryStream stream = new(data);

        ulong hash = MovieHashHelper.ComputeMovieHash(stream, fileSize);

        // Expected: fileSize + 2 × (8192 × 0x0101010101010101). The product
        // overflows ulong by design — the OpenSubtitles algorithm uses wrapping
        // 64-bit add; wrap unchecked so the compiler doesn't reject the literal.
        const ulong byteChunk = 0x0101010101010101UL;
        const int chunksPerWindow = ChunkSize / 8;
        ulong expected = unchecked((ulong)fileSize + 2 * (ulong)chunksPerWindow * byteChunk);

        hash.Should().Be(expected);
    }

    [Fact]
    public void ComputeMovieHash_FileSmallerThanChunk_OverlapsHeadAndTail()
    {
        // 32KB file < 64KB ChunkSize: head and tail both read from offset 0.
        // The accumulation counts the same bytes twice — but tailOffset is
        // clamped to Math.Max(0, fileSize - ChunkSize) = 0, so tail reads
        // from position 0 too, reading the same bytes.
        const int fileSize = 32 * 1024;
        byte[] data = new byte[fileSize];
        Array.Fill(data, (byte)0x02);
        using MemoryStream stream = new(data);

        ulong hash = MovieHashHelper.ComputeMovieHash(stream, fileSize);

        const ulong byteChunk = 0x0202020202020202UL;
        const int chunksInFile = fileSize / 8;
        ulong expected = unchecked((ulong)fileSize + 2 * (ulong)chunksInFile * byteChunk);

        hash.Should().Be(expected);
    }

    [Fact]
    public void ComputeMovieHash_NotFullyDivisibleBy8_DropsTrailingBytes()
    {
        // 13-byte file: only 8 bytes (one chunk) get accumulated; trailing 5 ignored.
        byte[] data = [1, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        using MemoryStream stream = new(data);

        ulong hash = MovieHashHelper.ComputeMovieHash(stream, data.Length);

        // First 8 bytes LE = 0x0000_0000_0000_0001
        // Both head and tail read the same 13 bytes (overlap, small file)
        // Each accumulates 0x01.
        const ulong leOne = 0x0000_0000_0000_0001UL;
        ulong expected = (ulong)data.Length + 2 * leOne;

        hash.Should().Be(expected);
    }

    [Theory]
    [InlineData([0UL, "0000000000000000"])]
    [InlineData([0xDEADBEEFUL, "00000000deadbeef"])]
    [InlineData([0x1234567890ABCDEFUL, "1234567890abcdef"])]
    public void FormatHash_FormatsAsLowercaseHex16(ulong hash, string expected)
    {
        MovieHashHelper.FormatHash(hash).Should().Be(expected);
    }

    [Fact]
    public void FormatHash_LeadingZeros_Preserved()
    {
        // OpenSubtitles requires 16-char hex even for small hashes.
        string formatted = MovieHashHelper.FormatHash(0x10UL);
        formatted.Should().HaveLength(16);
        formatted.Should().StartWith("000000000000");
    }
}
