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

namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Single source of truth for "given an FFmpeg encoder name, what codec family
/// does it belong to?" Same logic was previously reimplemented in three places
/// — HlsCodecsStringBuilder, V2ProfileFactory.ParseVideoCodec, and
/// PlanStage.VideoCodecFamilyToken — each with slightly different fallbacks
/// and odd cases. Funnelling them through this classifier means future encoder
/// names (e.g. AMD AV1 variants, AppleSilicon RT codecs) only need to be
/// taught once.
/// </summary>
public static class CodecFamilyClassifier
{
    /// <summary>
    /// Classifies a video encoder name into its <see cref="VideoCodecType"/>.
    /// Returns null if the encoder string doesn't match any known family —
    /// callers decide whether to fall back or throw.
    /// </summary>
    public static VideoCodecType? ClassifyVideo(string encoderName)
    {
        if (string.IsNullOrWhiteSpace(value: encoderName))
            return null;

        string lower = encoderName.ToLowerInvariant();

        if (lower == "copy" || lower.Contains(value: "passthrough"))
            return VideoCodecType.Copy;
        if (lower.Contains(value: "264") || lower.Contains(value: "avc") || lower.Contains(value: "h264"))
            return VideoCodecType.H264;
        if (lower.Contains(value: "265") || lower.Contains(value: "hevc"))
            return VideoCodecType.H265;
        if (lower.Contains(value: "av1") || lower.Contains(value: "aom") || lower.Contains(value: "svtav1"))
            return VideoCodecType.Av1;
        if (lower.Contains(value: "vp9") || lower.Contains(value: "libvpx"))
            return VideoCodecType.Vp9;

        return null;
    }

    /// <summary>
    /// Classifies an audio encoder name into its <see cref="AudioCodecType"/>.
    /// Returns null if the encoder string doesn't match any known family.
    /// </summary>
    public static AudioCodecType? ClassifyAudio(string encoderName)
    {
        if (string.IsNullOrWhiteSpace(value: encoderName))
            return null;

        string lower = encoderName.ToLowerInvariant();

        if (lower == "copy" || lower.Contains(value: "passthrough"))
            return AudioCodecType.Copy;
        if (lower.Contains(value: "aac") || lower.Contains(value: "fdk"))
            return AudioCodecType.Aac;
        if (lower.Contains(value: "opus"))
            return AudioCodecType.Opus;
        if (lower.Contains(value: "flac"))
            return AudioCodecType.Flac;
        if (lower.Contains(value: "eac3") || lower.Contains(value: "e-ac3"))
            return AudioCodecType.Eac3;
        if (lower.Contains(value: "ac3") || lower.Contains(value: "dolby"))
            return AudioCodecType.Ac3;
        if (lower.Contains(value: "mp3") || lower.Contains(value: "lame"))
            return AudioCodecType.Mp3;
        if (lower.Contains(value: "vorbis"))
            return AudioCodecType.Vorbis;
        if (lower.Contains(value: "truehd"))
            return AudioCodecType.TrueHd;
        if (lower.Contains(value: "dts") || lower.Contains(value: "dca"))
            return AudioCodecType.Dts;

        return null;
    }

    /// <summary>
    /// Short codec family token used in segment / folder names: <c>avc</c>,
    /// <c>hevc</c>, <c>av1</c>, <c>vp9</c>. Falls back to the lowercased
    /// encoder name when no family matches.
    /// </summary>
    public static string FamilyToken(string encoderName) =>
        ClassifyVideo(encoderName: encoderName) switch
        {
            VideoCodecType.H264 => "avc",
            VideoCodecType.H265 => "hevc",
            VideoCodecType.Av1 => "av1",
            VideoCodecType.Vp9 => "vp9",
            _ => encoderName.ToLowerInvariant(),
        };
}
