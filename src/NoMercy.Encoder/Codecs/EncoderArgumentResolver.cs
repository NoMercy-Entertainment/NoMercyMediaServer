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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Maps profile-level encoding parameters (preset, profile, CRF) to the correct
/// FFmpeg arguments for the resolved encoder. Profiles are written in libx264 terms
/// (e.g. preset=fast, profile=high, crf=22) but the actual encoder may be NVENC,
/// AMF, QSV, VAAPI, or VideoToolbox — each with different flag names and value sets.
/// </summary>
public static class EncoderArgumentResolver
{
    /// <summary>
    /// Returns the validated preset for the target encoder.
    /// Falls back to the encoder's middle preset if the profile preset is unsupported.
    /// </summary>
    public static string? ResolvePreset(string? profilePreset, EncoderInfo encoder)
    {
        if (encoder.Presets.Length == 0)
            return null; // Encoder has no preset concept (e.g. VAAPI)

        if (profilePreset is not null && encoder.Presets.Contains(profilePreset))
            return profilePreset;

        // Default to middle preset (balanced speed/quality)
        return encoder.Presets[encoder.Presets.Length / 2];
    }

    /// <summary>
    /// Returns the validated profile for the target encoder.
    /// Falls back to the encoder's first (safest) profile if unsupported.
    /// </summary>
    public static string? ResolveProfile(string? profileValue, EncoderInfo encoder)
    {
        if (encoder.Profiles.Length == 0)
            return null;

        if (profileValue is not null && encoder.Profiles.Contains(profileValue))
            return profileValue;

        return encoder.Profiles[0];
    }

    /// <summary>
    /// Maps CRF to the correct rate control flag for the encoder.
    /// Software encoders use -crf. Hardware encoders use -cq (NVENC), -qp (AMF/QSV),
    /// -global_quality (Intel ICQ), or -q:v (VideoToolbox).
    ///
    /// Returns the CRF value to pass to OutputOptions (0 if moved to extraFlags)
    /// and populates extraFlags with the hardware-specific quality flag.
    /// </summary>
    public static int ResolveQuality(
        int profileCrf,
        ResolvedCodec resolved,
        Dictionary<string, string> extraFlags
    )
    {
        if (profileCrf <= 0)
            return 0;

        // Stream copy passthrough has no quality dial. Profiles that
        // accidentally carry a non-zero CRF on a Copy output must not have
        // it forwarded as -qp / -cq / -crf — `ffmpeg -c:v copy` rejects
        // every quality flag and refuses to start the encode.
        if (resolved.FfmpegEncoderName == "copy")
            return 0;

        bool supportsCrf = resolved.EncoderInfo.SupportedRateControl.Contains(RateControlMode.Crf);
        if (supportsCrf)
            return profileCrf; // Software encoder — use -crf as-is

        // Hardware encoder — map to the correct quality flag with rate control mode.
        // Scale the profile's CRF value into the encoder's native quality range.
        // Without this, a profile written in libsvtav1 terms (Crf=35 on a 0-63 scale)
        // would reach av1_amf as "-qp 35" on its 0-255 scale (near-lossless) —
        // same CRF number, wildly different output size.
        int scaledQuality = ScaleQualityToEncoder(profileCrf, resolved.EncoderInfo);
        string qualityString = scaledQuality.ToString();

        switch (resolved.DefaultRateControl)
        {
            case RateControlMode.Cq:
                // NVENC: -rc vbr -cq VALUE
                extraFlags["-rc"] = "vbr";
                extraFlags["-cq"] = qualityString;
                break;
            case RateControlMode.Icq:
                // Intel QSV: -global_quality VALUE
                extraFlags["-global_quality"] = qualityString;
                break;
            case RateControlMode.QualityLevel:
                // VideoToolbox: -q:v VALUE
                extraFlags["-q:v"] = qualityString;
                break;
            default:
                // AMF/VAAPI: -rc cqp -qp VALUE
                extraFlags["-rc"] = "cqp";
                extraFlags["-qp"] = qualityString;
                break;
        }
        return 0; // Don't emit -crf
    }

    /// <summary>
    /// Scales a profile-level CRF value (always in "software-encoder" units for
    /// the codec) into the target encoder's native quality range. Proportional
    /// linear mapping — perceptually it's a rough approximation (CRF vs QP
    /// curves differ per encoder), but orders of magnitude closer than passing
    /// raw values. Clamped to the encoder's [Min, Max] so the emitted value is
    /// always accepted by ffmpeg.
    /// </summary>
    internal static int ScaleQualityToEncoder(int profileCrf, EncoderInfo encoder)
    {
        QualityRange range = encoder.QualityRange;

        // Reference ranges — the "software encoder" scale the profile was written in.
        // If the target encoder shares the same max, pass through to avoid
        // floating-point drift for the common case.
        int referenceMax = InferReferenceMax(encoder);
        if (range.Max == referenceMax)
            return Math.Clamp(profileCrf, range.Min, range.Max);

        double ratio = (double)profileCrf / referenceMax;
        int scaled = (int)Math.Round(ratio * range.Max);
        return Math.Clamp(scaled, Math.Max(1, range.Min), range.Max);
    }

    /// <summary>
    /// Returns the "reference" quality max for the codec family the encoder
    /// belongs to. H264 / HEVC / most QSV variants use 0-51. AV1 / VP9
    /// software uses 0-63. The profile is written in these reference units
    /// so scaling target the same point on the quality curve.
    /// </summary>
    private static int InferReferenceMax(EncoderInfo encoder)
    {
        // Heuristic based on encoder name — the codec family determines the
        // reference scale. Avoids taking a CodecRegistry dependency here.
        string name = encoder.FfmpegName.ToLowerInvariant();
        if (name.Contains("av1") || name.StartsWith("libsvtav1") || name.StartsWith("libaom"))
            return 63;
        if (name.Contains("vp9") || name.Contains("libvpx"))
            return 63;
        // H264, HEVC, and anything else — 0-51 reference.
        return 51;
    }

    /// <summary>
    /// Resolves output dimensions. Never upscales beyond source. Ensures even height.
    /// </summary>
    public static (int width, int height) ResolveDimensions(
        VideoOutput profile,
        int sourceWidth,
        int sourceHeight
    )
    {
        int outputWidth = Math.Min(profile.Width, sourceWidth);
        // Height <= 0 means "derive from source AR" just like null: ladder rungs
        // carry a non-nullable int, so an upstream null collapses to 0 — a literal
        // 0 here would name the variant "WIDTHx0" and advertise RESOLUTION=WIDTHx0,
        // which players skip.
        int outputHeight =
            profile.Height is int explicitHeight and > 0 ? explicitHeight
            : sourceWidth > 0 ? outputWidth * sourceHeight / sourceWidth
            : 0;

        // Encoders require even dimensions
        if (outputHeight % 2 != 0)
            outputHeight++;

        return (outputWidth, outputHeight);
    }
}
