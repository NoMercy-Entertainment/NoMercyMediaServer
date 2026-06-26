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

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Maximum luma samples per second (width × height × fps) permitted by each
/// codec level. Used by the HFR validator rule to detect configurations where
/// the declared level cannot sustain the source frame rate at the target
/// resolution. Values are taken from:
/// - H.264: ITU-T H.264 Table A-1 (max luma sample rate per level)
/// - HEVC: ITU-T H.265 Table A-1
/// - VP9: WebM VP9 Bitstream Specification Table 1
/// </summary>
public static class CodecLevelFpsCaps
{
    /// <param name="Level">The codec-level string as it appears in EncodingProfile (e.g. "4.1", "5.0").</param>
    /// <param name="MaxLumaSamplesPerSec">The published upper bound from the spec.</param>
    public record LevelCap(string Level, long MaxLumaSamplesPerSec);

    public static readonly IReadOnlyList<LevelCap> H264 =
    [
        new("1.0", 380_160),
        new("1.1", 768_000),
        new("1.2", 1_536_000),
        new("1.3", 3_041_280),
        new("2.0", 3_041_280),
        new("2.1", 5_068_800),
        new("2.2", 5_184_000),
        new("3.0", 10_368_000),
        new("3.1", 27_648_000),
        new("3.2", 55_296_000),
        new("4.0", 62_914_560),
        new("4.1", 62_914_560),
        new("4.2", 133_693_440),
        new("5.0", 150_994_944),
        new("5.1", 251_658_240),
        new("5.2", 530_841_600),
        new("6.0", 1_069_547_520),
        new("6.1", 2_139_095_040),
        new("6.2", 4_278_190_080),
    ];

    public static readonly IReadOnlyList<LevelCap> Hevc =
    [
        new("1.0", 552_960),
        new("2.0", 3_686_400),
        new("2.1", 7_372_800),
        new("3.0", 16_588_800),
        new("3.1", 33_177_600),
        new("4.0", 66_846_720),
        new("4.1", 133_693_440),
        new("5.0", 267_386_880),
        new("5.1", 534_773_760),
        new("5.2", 1_069_547_520),
        new("6.0", 1_069_547_520),
        new("6.1", 2_139_095_040),
        new("6.2", 4_278_190_080),
    ];

    // VP9 profile levels follow the WebM container spec level definitions.
    // Each level caps at the listed max picture size × frame rate product.
    public static readonly IReadOnlyList<LevelCap> Vp9 =
    [
        new("1", 829_440),
        new("1.1", 2_764_800),
        new("2", 4_608_000),
        new("2.1", 9_216_000),
        new("3", 20_736_000),
        new("3.1", 36_864_000),
        new("4", 83_558_400),
        new("4.1", 160_432_128),
        new("5", 311_951_360),
        new("5.1", 588_251_136),
        new("5.2", 1_176_502_272),
        new("6", 1_176_502_272),
        new("6.1", 4_706_009_088),
        new("6.2", 9_412_018_176),
    ];

    /// <summary>
    /// Returns the <see cref="LevelCap"/> for a given codec and level string,
    /// or null when the level is not in the table (unknown / future level).
    /// </summary>
    public static LevelCap? Lookup(VideoCodecType codec, string level)
    {
        IReadOnlyList<LevelCap> table = codec switch
        {
            VideoCodecType.H264 => H264,
            VideoCodecType.H265 => Hevc,
            VideoCodecType.Vp9 => Vp9,
            _ => [],
        };

        return table.FirstOrDefault(c =>
            string.Equals(c.Level, level, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Returns the first level in the table for the given codec whose
    /// <see cref="LevelCap.MaxLumaSamplesPerSec"/> is at least
    /// <paramref name="requiredSamplesPerSec"/>, or null when none fits.
    /// </summary>
    public static LevelCap? FindNextFit(VideoCodecType codec, long requiredSamplesPerSec)
    {
        IReadOnlyList<LevelCap> table = codec switch
        {
            VideoCodecType.H264 => H264,
            VideoCodecType.H265 => Hevc,
            VideoCodecType.Vp9 => Vp9,
            _ => [],
        };

        return table.FirstOrDefault(c => c.MaxLumaSamplesPerSec >= requiredSamplesPerSec);
    }
}
