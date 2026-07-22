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
/// Turns an <see cref="EncodingQuality"/> tier into per-codec settings. CRF 22
/// does not mean the same thing to x264, x265, SVT-AV1 and libvpx, so presets
/// pick a tier and this decides what that costs on each encoder.
/// </summary>
public static class CodecTunings
{
    public record CodecTuning(
        int Crf,
        string Preset,
        CodecProfile Profile,
        int BitDepth,
        Dictionary<string, string>? ExtraArgs = null
    );

    public static CodecTuning For(VideoCodecType codec, EncodingQuality quality) =>
        codec switch
        {
            VideoCodecType.H264 => ForH264(q: quality),
            VideoCodecType.H265 => ForH265(q: quality),
            VideoCodecType.Av1 => ForAv1(q: quality),
            VideoCodecType.Vp9 => ForVp9(q: quality),
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(codec), actualValue: codec, message: null),
        };

    private static CodecTuning ForH264(EncodingQuality q) =>
        q switch
        {
            EncodingQuality.Archive => new(
                Crf: 18,
                Preset: "veryslow",
                Profile: CodecProfile.High,
                BitDepth: 8,
                ExtraArgs: new()
                {
                    [key: "x264.aq-mode"] = "3",
                    [key: "x264.rc-lookahead"] = "60",
                    [key: "x264.ref"] = "5",
                    [key: "x264.psy-rd"] = "1.0,0.15",
                }
            ),
            EncodingQuality.Ultra => new(
                Crf: 18,
                Preset: "slower",
                Profile: CodecProfile.High,
                BitDepth: 8,
                ExtraArgs: new() { [key: "x264.aq-mode"] = "3" }
            ),
            EncodingQuality.VeryHigh => new(Crf: 20, Preset: "slow", Profile: CodecProfile.High, BitDepth: 8),
            EncodingQuality.High => new(Crf: 22, Preset: "slow", Profile: CodecProfile.High, BitDepth: 8),
            EncodingQuality.Balanced => new(Crf: 23, Preset: "medium", Profile: CodecProfile.High, BitDepth: 8),
            EncodingQuality.Streaming => new(Crf: 24, Preset: "medium", Profile: CodecProfile.High, BitDepth: 8),
            EncodingQuality.Fast => new(Crf: 26, Preset: "fast", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.Preview => new(Crf: 28, Preset: "veryfast", Profile: CodecProfile.Baseline, BitDepth: 8),
            _ => new(Crf: 23, Preset: "medium", Profile: CodecProfile.High, BitDepth: 8),
        };

    private static CodecTuning ForH265(EncodingQuality q) =>
        q switch
        {
            EncodingQuality.Archive => new(
                Crf: 18,
                Preset: "slower",
                Profile: CodecProfile.Main10,
                BitDepth: 10,
                ExtraArgs: new() { [key: "x265.rdoq-level"] = "2", [key: "x265.aq-mode"] = "3" }
            ),
            EncodingQuality.Ultra => new(Crf: 18, Preset: "slower", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.VeryHigh => new(Crf: 20, Preset: "slow", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.High => new(Crf: 22, Preset: "slow", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Balanced => new(Crf: 24, Preset: "medium", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Streaming => new(Crf: 24, Preset: "medium", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Fast => new(Crf: 26, Preset: "fast", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Preview => new(Crf: 30, Preset: "faster", Profile: CodecProfile.Main10, BitDepth: 10),
            _ => new(Crf: 24, Preset: "medium", Profile: CodecProfile.Main10, BitDepth: 10),
        };

    private static CodecTuning ForAv1(EncodingQuality q) =>
        q switch
        {
            EncodingQuality.Archive => new(
                Crf: 18,
                Preset: "2",
                Profile: CodecProfile.Main10,
                BitDepth: 10,
                ExtraArgs: new() { [key: "svtav1.tune"] = "0", [key: "svtav1.enable-qm"] = "1" }
            ),
            EncodingQuality.Ultra => new(Crf: 22, Preset: "3", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.VeryHigh => new(Crf: 26, Preset: "4", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.High => new(Crf: 28, Preset: "4", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Balanced => new(Crf: 30, Preset: "5", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Streaming => new(Crf: 32, Preset: "6", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Fast => new(Crf: 34, Preset: "7", Profile: CodecProfile.Main10, BitDepth: 10),
            EncodingQuality.Preview => new(Crf: 36, Preset: "8", Profile: CodecProfile.Main10, BitDepth: 10),
            _ => new(Crf: 30, Preset: "5", Profile: CodecProfile.Main10, BitDepth: 10),
        };

    private static CodecTuning ForVp9(EncodingQuality q) =>
        q switch
        {
            EncodingQuality.Archive => new(
                Crf: 30,
                Preset: "good",
                Profile: CodecProfile.Main,
                BitDepth: 8,
                ExtraArgs: new()
                {
                    [key: "vp9.cpu-used"] = "0",
                    [key: "vp9.row-mt"] = "1",
                    [key: "vp9.lag-in-frames"] = "25",
                }
            ),
            EncodingQuality.Ultra => new(Crf: 30, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.VeryHigh => new(Crf: 32, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.High => new(Crf: 34, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.Balanced => new(Crf: 36, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.Streaming => new(Crf: 38, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.Fast => new(Crf: 40, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            EncodingQuality.Preview => new(Crf: 44, Preset: "realtime", Profile: CodecProfile.Main, BitDepth: 8),
            _ => new(Crf: 36, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
        };
}
