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

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Audio -af filter-string assembly extracted from PlanStage: pan (downmix)
/// and loudnorm filter selection, combined into a single filter chain.
/// </summary>
internal static class AudioFilterBuilder
{
    /// <summary>
    /// Builds a single FFmpeg audio-filter chain: <c>pan=</c> (when an explicit
    /// downmix matrix is requested) chained with <c>loudnorm</c> (when a
    /// loudness target is requested). Pan runs first because loudnorm expects
    /// the final channel layout. Returns null when neither filter is needed.
    /// </summary>
    internal static string? BuildAudioFilter(
        LoudnessMode loudness,
        DownmixMode downmix,
        string? customPanMatrix
    )
    {
        string? pan = BuildPanFilter(mode: downmix, customPanMatrix: customPanMatrix);
        string? loudnorm = BuildLoudnormFilter(loudness: loudness);

        return (pan, loudnorm) switch
        {
            (null, null) => null,
            (not null, null) => pan,
            (null, not null) => loudnorm,
            _ => $"{pan},{loudnorm}",
        };
    }

    private static string? BuildPanFilter(DownmixMode mode, string? customPanMatrix) =>
        mode switch
        {
            // ITU-R BS.775 5.1 → stereo. Center folded at -3 dB, surrounds at -3 dB.
            DownmixMode.StereoItuR128 =>
                "pan=stereo|FL<FL+0.707*FC+0.707*BL+0.707*SL|FR<FR+0.707*FC+0.707*BR+0.707*SR",
            // Simple equal-weight sum; safe for any input channel layout.
            DownmixMode.Mono => "pan=mono|c0<0.5*FL+0.5*FR+0.5*FC+0.25*BL+0.25*BR+0.25*SL+0.25*SR",
            DownmixMode.Custom => string.IsNullOrWhiteSpace(value: customPanMatrix)
                ? null
                : $"pan={customPanMatrix}",
            _ => null,
        };

    private static string? BuildLoudnormFilter(LoudnessMode loudness) =>
        loudness switch
        {
            // EBU R128 streaming target: -16 LUFS integrated, -1.5 dBTP true peak, 11 LU LRA.
            LoudnessMode.EbuR128 => "loudnorm=I=-16:TP=-1.5:LRA=11",
            // ReplayGain target: -18 LUFS integrated, same peak + range as R128.
            LoudnessMode.ReplayGain => "loudnorm=I=-18:TP=-1.5:LRA=11",
            // Custom loudnorm left to CustomArguments on the profile; no auto filter here.
            LoudnessMode.Custom => null,
            _ => null,
        };
}
