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
        new(Level: "1.0", MaxLumaSamplesPerSec: 380_160),
        new(Level: "1.1", MaxLumaSamplesPerSec: 768_000),
        new(Level: "1.2", MaxLumaSamplesPerSec: 1_536_000),
        new(Level: "1.3", MaxLumaSamplesPerSec: 3_041_280),
        new(Level: "2.0", MaxLumaSamplesPerSec: 3_041_280),
        new(Level: "2.1", MaxLumaSamplesPerSec: 5_068_800),
        new(Level: "2.2", MaxLumaSamplesPerSec: 5_184_000),
        new(Level: "3.0", MaxLumaSamplesPerSec: 10_368_000),
        new(Level: "3.1", MaxLumaSamplesPerSec: 27_648_000),
        new(Level: "3.2", MaxLumaSamplesPerSec: 55_296_000),
        new(Level: "4.0", MaxLumaSamplesPerSec: 62_914_560),
        new(Level: "4.1", MaxLumaSamplesPerSec: 62_914_560),
        new(Level: "4.2", MaxLumaSamplesPerSec: 133_693_440),
        new(Level: "5.0", MaxLumaSamplesPerSec: 150_994_944),
        new(Level: "5.1", MaxLumaSamplesPerSec: 251_658_240),
        new(Level: "5.2", MaxLumaSamplesPerSec: 530_841_600),
        new(Level: "6.0", MaxLumaSamplesPerSec: 1_069_547_520),
        new(Level: "6.1", MaxLumaSamplesPerSec: 2_139_095_040),
        new(Level: "6.2", MaxLumaSamplesPerSec: 4_278_190_080),
    ];

    public static readonly IReadOnlyList<LevelCap> Hevc =
    [
        new(Level: "1.0", MaxLumaSamplesPerSec: 552_960),
        new(Level: "2.0", MaxLumaSamplesPerSec: 3_686_400),
        new(Level: "2.1", MaxLumaSamplesPerSec: 7_372_800),
        new(Level: "3.0", MaxLumaSamplesPerSec: 16_588_800),
        new(Level: "3.1", MaxLumaSamplesPerSec: 33_177_600),
        new(Level: "4.0", MaxLumaSamplesPerSec: 66_846_720),
        new(Level: "4.1", MaxLumaSamplesPerSec: 133_693_440),
        new(Level: "5.0", MaxLumaSamplesPerSec: 267_386_880),
        new(Level: "5.1", MaxLumaSamplesPerSec: 534_773_760),
        new(Level: "5.2", MaxLumaSamplesPerSec: 1_069_547_520),
        new(Level: "6.0", MaxLumaSamplesPerSec: 1_069_547_520),
        new(Level: "6.1", MaxLumaSamplesPerSec: 2_139_095_040),
        new(Level: "6.2", MaxLumaSamplesPerSec: 4_278_190_080),
    ];

    // VP9 profile levels follow the WebM container spec level definitions.
    // Each level caps at the listed max picture size × frame rate product.
    public static readonly IReadOnlyList<LevelCap> Vp9 =
    [
        new(Level: "1", MaxLumaSamplesPerSec: 829_440),
        new(Level: "1.1", MaxLumaSamplesPerSec: 2_764_800),
        new(Level: "2", MaxLumaSamplesPerSec: 4_608_000),
        new(Level: "2.1", MaxLumaSamplesPerSec: 9_216_000),
        new(Level: "3", MaxLumaSamplesPerSec: 20_736_000),
        new(Level: "3.1", MaxLumaSamplesPerSec: 36_864_000),
        new(Level: "4", MaxLumaSamplesPerSec: 83_558_400),
        new(Level: "4.1", MaxLumaSamplesPerSec: 160_432_128),
        new(Level: "5", MaxLumaSamplesPerSec: 311_951_360),
        new(Level: "5.1", MaxLumaSamplesPerSec: 588_251_136),
        new(Level: "5.2", MaxLumaSamplesPerSec: 1_176_502_272),
        new(Level: "6", MaxLumaSamplesPerSec: 1_176_502_272),
        new(Level: "6.1", MaxLumaSamplesPerSec: 4_706_009_088),
        new(Level: "6.2", MaxLumaSamplesPerSec: 9_412_018_176),
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

        return table.FirstOrDefault(predicate: c =>
            string.Equals(a: c.Level, b: level, comparisonType: StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// True when the codec has a known-level table, so a level not found by
    /// <see cref="Lookup"/> is genuinely invalid (rather than merely a codec
    /// whose levels this catalogue does not enumerate, e.g. AV1).
    /// </summary>
    public static bool HasLevelTable(VideoCodecType codec) =>
        codec is VideoCodecType.H264 or VideoCodecType.H265 or VideoCodecType.Vp9;

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

        return table.FirstOrDefault(predicate: c => c.MaxLumaSamplesPerSec >= requiredSamplesPerSec);
    }
}
