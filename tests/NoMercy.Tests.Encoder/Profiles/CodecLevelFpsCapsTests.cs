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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// Codec-level max-luma-samples-per-second tables. These values come from the
/// codec specs (H.264 Table A-1, H.265 Table A-1, WebM VP9 §1) and must match
/// exactly — encoders reject streams that declare a level smaller than their
/// content needs, so a wrong table entry produces a confusing "encoder
/// refused the stream" error at runtime.
/// </summary>
public class CodecLevelFpsCapsTests
{
    // ── H.264 spec values ───────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["4.0", 62_914_560L])] // 1080p30 needs ~62M
    [InlineData(data: ["4.1", 62_914_560L])] // Same as 4.0
    [InlineData(data: ["4.2", 133_693_440L])] // 1080p60
    [InlineData(data: ["5.0", 150_994_944L])] // Up to ~4K30 in theory
    [InlineData(data: ["5.1", 251_658_240L])] // 4K30 typical declared
    [InlineData(data: ["5.2", 530_841_600L])] // 4K60
    [InlineData(data: ["6.0", 1_069_547_520L])] // 8K30
    [InlineData(data: ["6.2", 4_278_190_080L])] // 8K120
    public void H264_Lookup_SpecValues(string level, long expected)
    {
        CodecLevelFpsCaps
            .Lookup(codec: VideoCodecType.H264, level: level)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(predicate: c => c.MaxLumaSamplesPerSec == expected);
    }

    // ── HEVC spec values ────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["3.1", 33_177_600L])] // 720p30
    [InlineData(data: ["4.0", 66_846_720L])] // 1080p30 typical
    [InlineData(data: ["4.1", 133_693_440L])] // 1080p60
    [InlineData(data: ["5.0", 267_386_880L])] // 4K30
    [InlineData(data: ["5.1", 534_773_760L])] // 4K60
    [InlineData(data: ["5.2", 1_069_547_520L])] // 8K30
    public void Hevc_Lookup_SpecValues(string level, long expected)
    {
        CodecLevelFpsCaps
            .Lookup(codec: VideoCodecType.H265, level: level)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(predicate: c => c.MaxLumaSamplesPerSec == expected);
    }

    // ── VP9 spec values ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["3.1", 36_864_000L])]
    [InlineData(data: ["4", 83_558_400L])]
    [InlineData(data: ["5", 311_951_360L])]
    [InlineData(data: ["5.1", 588_251_136L])]
    public void Vp9_Lookup_SpecValues(string level, long expected)
    {
        CodecLevelFpsCaps
            .Lookup(codec: VideoCodecType.Vp9, level: level)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(predicate: c => c.MaxLumaSamplesPerSec == expected);
    }

    // ── Lookup edge cases ───────────────────────────────────────────────────

    [Fact]
    public void Lookup_UnknownLevel_ReturnsNull()
    {
        CodecLevelFpsCaps.Lookup(codec: VideoCodecType.H264, level: "9.9").Should().BeNull();
    }

    [Fact]
    public void Lookup_UnsupportedCodec_ReturnsNull()
    {
        // AV1 / Copy / etc. have no entries in this table yet.
        CodecLevelFpsCaps.Lookup(codec: VideoCodecType.Av1, level: "5.0").Should().BeNull();
        CodecLevelFpsCaps.Lookup(codec: VideoCodecType.Copy, level: "5.0").Should().BeNull();
    }

    [Fact]
    public void Lookup_CaseInsensitive()
    {
        // Level strings can come from user input or DB rows — accept any case.
        CodecLevelFpsCaps.Lookup(codec: VideoCodecType.H264, level: "4.0").Should().NotBeNull();
        CodecLevelFpsCaps
            .Lookup(codec: VideoCodecType.H264, level: "4.0".ToUpperInvariant())
            .Should()
            .NotBeNull();
    }

    // ── FindNextFit ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: [VideoCodecType.H264, 62_914_560L, "4.0"])] // Exact-match boundary
    [InlineData(data: [VideoCodecType.H264, 70_000_000L, "4.2"])] // Bumps to next level
    [InlineData(data: [VideoCodecType.H264, 530_841_600L, "5.2"])] // 4K60ish exact-match boundary
    [InlineData(data: [VideoCodecType.H265, 100_000_000L, "4.1"])] // HEVC 4.1 = 133M
    [InlineData(data: [VideoCodecType.H265, 500_000_000L, "5.1"])] // 4K60 HEVC
    public void FindNextFit_PicksFirstSufficientLevel(
        VideoCodecType codec,
        long required,
        string expected
    )
    {
        CodecLevelFpsCaps
            .FindNextFit(codec: codec, requiredSamplesPerSec: required)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(predicate: c => c.Level == expected);
    }

    [Fact]
    public void FindNextFit_ExceedsHighestLevel_ReturnsNull()
    {
        // H.264 6.2 caps at 4.28B; ask for more.
        CodecLevelFpsCaps.FindNextFit(codec: VideoCodecType.H264, requiredSamplesPerSec: 5_000_000_000L).Should().BeNull();
    }

    [Fact]
    public void FindNextFit_UnsupportedCodec_ReturnsNull()
    {
        CodecLevelFpsCaps.FindNextFit(codec: VideoCodecType.Av1, requiredSamplesPerSec: 100_000_000L).Should().BeNull();
    }

    // ── Table integrity ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: VideoCodecType.H264)]
    [InlineData(data: VideoCodecType.H265)]
    [InlineData(data: VideoCodecType.Vp9)]
    public void Tables_AreMonotonicallyIncreasing(VideoCodecType codec)
    {
        // FindNextFit relies on tables being ordered low→high so the first
        // match is the smallest sufficient level. Any out-of-order entry
        // breaks that contract silently.
        IReadOnlyList<CodecLevelFpsCaps.LevelCap> table = codec switch
        {
            VideoCodecType.H264 => CodecLevelFpsCaps.H264,
            VideoCodecType.H265 => CodecLevelFpsCaps.Hevc,
            VideoCodecType.Vp9 => CodecLevelFpsCaps.Vp9,
            _ => [],
        };

        for (int i = 1; i < table.Count; i++)
        {
            // Allow equal (4.0 / 4.1 in H.264 share the same cap by design).
            table[index: i]
                .MaxLumaSamplesPerSec.Should()
                .BeGreaterThanOrEqualTo(
                    expected: table[index: i - 1].MaxLumaSamplesPerSec,
                    because: $"{codec} table entry {table[index: i].Level} must be >= preceding "
                             + $"{table[index: i - 1].Level}"
                );
        }
    }
}
