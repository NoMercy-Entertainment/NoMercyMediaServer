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

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Static helpers shared by pipeline stages that iterate
/// <see cref="EncodingProfile"/> stream outputs.
/// </summary>
internal static class PlanStageHelpers
{
    /// <summary>
    /// Returns the effective set of <see cref="VideoOutput"/> entries for an
    /// <see cref="EncodingProfile"/>, expanding ladder rungs when the ladder is in Manual mode.
    ///
    /// <list type="bullet">
    ///   <item>No Video → empty array.</item>
    ///   <item>Ladder.Mode == Manual with rungs → each rung materialised to
    ///     a VideoOutput derived from the base Video output.</item>
    ///   <item>Otherwise → single-element array containing Video.</item>
    /// </list>
    ///
    /// Auto-ladder expansion (LadderMode.Auto) is handled by PlanStage via
    /// <see cref="IAbrLadderGenerator"/>, which produces the final
    /// VideoOutput[] and replaces the profile reference before this helper
    /// is called. So by the time <c>EnumerateVideo</c> runs on the expanded
    /// profile, the Ladder is already null or Manual with materialised rungs.
    /// </summary>
    internal static VideoOutput[] EnumerateVideo(EncodingProfile profile)
    {
        if (
            profile.Ladder is { Mode: LadderMode.Manual, Rungs: { Length: > 0 } rungs }
        )
        {
            VideoOutput reference = profile.Video ?? BuildSyntheticReference(rungs[0]);
            return rungs.Select(r => RungToVideoOutput(r, reference)).ToArray();
        }

        if (profile.Video is null)
            return [];

        return [profile.Video];
    }

    /// <summary>
    /// When no <see cref="VideoOutput"/> reference is provided alongside a
    /// <see cref="LadderConfig"/>, synthesise a minimal reference from the
    /// first rung using safe defaults for fields the rung doesn't carry.
    /// </summary>
    internal static VideoOutput BuildSyntheticReference(LadderRung rung) =>
        new(
            StreamPolicy.Transcode,
            rung.Codec,
            rung.Width,
            rung.Height,
            Profiles.RateControlMode.Crf,
            23,
            rung.BitrateKbps,
            rung.MaxBitrateKbps > 0 ? rung.MaxBitrateKbps : null,
            rung.BufferSizeKbps > 0 ? rung.BufferSizeKbps : null,
            rung.Preset,
            rung.CodecProfile,
            null,
            null,
            rung.BitDepth,
            rung.PixelFormat,
            2,
            false,
            "video/{label}",
            "video/{label}/playlist"
        );

    /// <summary>Materialise one <see cref="LadderRung"/> into a full VideoOutput.</summary>
    internal static VideoOutput RungToVideoOutput(LadderRung rung, VideoOutput reference) =>
        reference with
        {
            Width = rung.Width,
            Height = rung.Height,
            Codec = rung.Codec,
            BitrateKbps = rung.BitrateKbps,
            MaxBitrateKbps = rung.MaxBitrateKbps,
            BufferSizeKbps = rung.BufferSizeKbps,
            BitDepth = rung.BitDepth,
            PixelFormat = rung.PixelFormat ?? reference.PixelFormat,
            Preset = rung.Preset ?? reference.Preset,
            CodecProfile = rung.CodecProfile,
        };

    /// <summary>
    /// Maps <see cref="Container"/> to the internal <see cref="OutputFormat"/>
    /// used by output strategies and <see cref="NoMercy.Encoder.Output.OutputPlan"/>.
    /// </summary>
    internal static OutputFormat ContainerToOutputFormat(Container container) =>
        container switch
        {
            Container.HlsTs => OutputFormat.Hls,
            Container.HlsFmp4 => OutputFormat.Hls,
            Container.AudioHlsTs => OutputFormat.AudioHls,
            Container.AudioHlsFmp4 => OutputFormat.AudioHls,
            Container.Mkv => OutputFormat.Mkv,
            Container.Mp4 => OutputFormat.Mp4,
            Container.Aac => OutputFormat.Mp4,
            Container.Dash => OutputFormat.Dash,
            Container.Mp3 => OutputFormat.Mp3,
            Container.Flac => OutputFormat.Flac,
            Container.Ogg => OutputFormat.Ogg,
            Container.Mka => OutputFormat.Mkv,
            Container.Mks => OutputFormat.Mkv,
            _ => OutputFormat.Hls,
        };
}
