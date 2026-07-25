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

using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Encoder.Output;

/// <summary>
/// Unions the per-preset <see cref="OutputPlan"/>s produced when a library
/// folder carries multiple encoding presets (e.g. a "4K HDR" ladder and a
/// separate "1080p SDR" ladder on the same folder) into ONE plan the
/// decomposition strategy can turn into a single coordinated encode — one
/// output folder, one master playlist listing every preset's video
/// rendition, with audio / subtitles / thumbnails / chapters produced once
/// and shared instead of once per preset.
/// </summary>
public static class OutputPlanMerger
{
    /// <summary>
    /// Merges <paramref name="plans"/> — already-planned <see cref="OutputPlan"/>s,
    /// one per preset, in the same order as the requests that produced them.
    /// <paramref name="plans"/>[0] is the PRIMARY plan: source-derived fields
    /// that are identical across presets (subtitles, thumbnails, chapters,
    /// bundle layout, DRM, HLS options, …) are taken from it rather than
    /// duplicated. Video renditions are the union of every plan's
    /// <see cref="OutputPlan.VideoOutputs"/>; audio renditions are unioned
    /// and deduplicated by (source stream, language, codec) so two presets that
    /// both carry "eng AAC 2.0" from the same source track don't produce it
    /// twice, while two distinct source tracks that share language + codec both
    /// survive.
    ///
    /// Every plan must share the same <see cref="OutputPlan.Format"/> — the
    /// caller (<see cref="Orchestration.EncodingOrchestrator.DecomposeMergedAsync"/>)
    /// is expected to have already verified this and fallen back to
    /// per-preset dispatch otherwise; this method throws
    /// <see cref="InvalidOperationException"/> as a defensive backstop.
    /// </summary>
    public static OutputPlan Merge(IReadOnlyList<OutputPlan> plans)
    {
        if (plans.Count == 0)
            throw new InvalidOperationException("Cannot merge an empty set of output plans.");

        OutputPlan primary = plans[0];

        if (plans.Any(plan => plan.Format != primary.Format))
            throw new InvalidOperationException(
                "Cannot merge output plans with different formats — "
                    + $"expected every plan to be {primary.Format}."
            );

        VideoOutputPlan[] mergedVideo = PlanStageDisambiguation.DisambiguateVideo(
            plans.SelectMany(plan => plan.VideoOutputs).ToArray()
        );

        AudioOutputPlan[] mergedAudio = PlanStageDisambiguation
            .DisambiguateAudio(DeduplicateAudio(plans.SelectMany(plan => plan.AudioOutputs)))
            .ToArray();

        return primary with
        {
            VideoOutputs = mergedVideo,
            AudioOutputs = mergedAudio,
        };
    }

    /// <summary>
    /// Unions audio renditions across presets, keeping the first occurrence of
    /// each distinct (source stream, language, codec) triple. Two presets both
    /// wanting "eng AAC 2.0" from the SAME source stream describe one rendition,
    /// not two — those dedup. But two DISTINCT source tracks that happen to share
    /// a language + codec (e.g. a movie with a 768k and a 960k English E-AC-3
    /// track) are separate renditions and MUST both survive: keying on
    /// (language, codec) alone silently dropped the second track (the reported
    /// missing-audio defect). The source stream index disambiguates them.
    /// </summary>
    private static List<AudioOutputPlan> DeduplicateAudio(IEnumerable<AudioOutputPlan> audios)
    {
        Dictionary<(int SourceStreamIndex, string Language, string Codec), AudioOutputPlan> seen =
        [];

        foreach (AudioOutputPlan audio in audios)
        {
            (int, string, string) key = (
                audio.SourceStreamIndex,
                audio.Language ?? "und",
                audio.CodecToken
            );
            seen.TryAdd(key, audio);
        }

        return seen.Values.ToList();
    }
}
