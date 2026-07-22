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
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Output-plan path collision resolution. When PlanStage emits multiple audio
/// or video plans whose resolved template land on the same on-disk path, this
/// helper appends a per-stream / per-codec suffix so the segments don't
/// overwrite each other.
/// </summary>
internal static class PlanStageDisambiguation
{
    private sealed record IndexedAudioPlan(AudioOutputPlan Plan, int Index);

    private sealed record AudioGroupKey(
        string Language,
        string CodecToken,
        string SegmentTemplate,
        string PlaylistTemplate
    );

    private sealed record IndexedVideoPlan(VideoOutputPlan Plan, int Index);

    private sealed record VideoGroupKey(
        int Width,
        int? Height,
        bool IsHdrOutput,
        string SegmentTemplate,
        string PlaylistTemplate
    );

    /// <summary>
    /// Detects audio plans whose resolved template would land on the same
    /// on-disk path (same language + same codec token) and appends a per-stream
    /// suffix derived from the MapLabel <c>0:a:N</c> source index. Single-
    /// stream-per-language sources see no change. Plans that already differ
    /// (different language, different codec, or already suffixed) are left
    /// alone.
    /// </summary>
    public static IEnumerable<AudioOutputPlan> DisambiguateAudio(
        IReadOnlyList<AudioOutputPlan> plans
    )
    {
        IEnumerable<IGrouping<AudioGroupKey, IndexedAudioPlan>> groups = plans
            .Select(selector: (plan, idx) => new IndexedAudioPlan(Plan: plan, Index: idx))
            .GroupBy(keySelector: entry => new AudioGroupKey(
                Language: entry.Plan.Language ?? "und",
                CodecToken: entry.Plan.CodecToken,
                SegmentTemplate: entry.Plan.SegmentNameTemplate,
                PlaylistTemplate: entry.Plan.PlaylistNameTemplate
            ));

        AudioOutputPlan[] result = plans.ToArray();
        foreach (IGrouping<AudioGroupKey, IndexedAudioPlan> group in groups)
        {
            if (group.Count() < 2)
                continue;

            foreach (IndexedAudioPlan entry in group)
            {
                int sourceIndex = ParseAudioSourceIndex(mapLabel: entry.Plan.MapLabel);
                string suffix = $"_{sourceIndex}";
                result[entry.Index] = entry.Plan with
                {
                    SegmentNameTemplate = AppendToTemplate(template: entry.Plan.SegmentNameTemplate, suffix: suffix),
                    PlaylistNameTemplate = AppendToTemplate(
                        template: entry.Plan.PlaylistNameTemplate,
                        suffix: suffix
                    ),
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Detects video plans whose resolved template would land on the same
    /// on-disk path (same dimensions + same HDR/SDR designation) and appends a
    /// codec-family suffix so two different-codec rungs at the same resolution
    /// (e.g. H.264 1080p fallback + HEVC 1080p tonemap under EmitHdrAndSdr)
    /// don't share <c>video_1920x1080_SDR/</c>.
    /// </summary>
    public static VideoOutputPlan[] DisambiguateVideo(IReadOnlyList<VideoOutputPlan> plans)
    {
        IEnumerable<IGrouping<VideoGroupKey, IndexedVideoPlan>> groups = plans
            .Select(selector: (plan, idx) => new IndexedVideoPlan(Plan: plan, Index: idx))
            .GroupBy(keySelector: entry => new VideoGroupKey(
                Width: entry.Plan.Width,
                Height: entry.Plan.Height,
                IsHdrOutput: entry.Plan.IsHdrOutput,
                SegmentTemplate: entry.Plan.SegmentNameTemplate,
                PlaylistTemplate: entry.Plan.PlaylistNameTemplate
            ));

        VideoOutputPlan[] result = plans.ToArray();
        foreach (IGrouping<VideoGroupKey, IndexedVideoPlan> group in groups)
        {
            if (group.Count() < 2)
                continue;

            foreach (IndexedVideoPlan entry in group)
            {
                string suffix = $"_{CodecFamilyClassifier.FamilyToken(encoderName: entry.Plan.EncoderName)}";
                result[entry.Index] = entry.Plan with
                {
                    SegmentNameTemplate = AppendToTemplate(template: entry.Plan.SegmentNameTemplate, suffix: suffix),
                    PlaylistNameTemplate = AppendToTemplate(
                        template: entry.Plan.PlaylistNameTemplate,
                        suffix: suffix
                    ),
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Appends a suffix to every <c>/</c>-separated segment in a template so
    /// both the directory and filename halves get disambiguated.
    /// <c>"audio_eng_aac/audio_eng_aac"</c> + <c>"_0"</c> becomes
    /// <c>"audio_eng_aac_0/audio_eng_aac_0"</c>, not
    /// <c>"audio_eng_aac/audio_eng_aac_0"</c> — the latter would still
    /// collide on the .m4s segment files since they live in the same dir.
    /// </summary>
    private static string AppendToTemplate(string template, string suffix) =>
        string.Join(separator: '/', values: template.Split(separator: '/').Select(selector: segment => segment + suffix));

    private static int ParseAudioSourceIndex(string mapLabel)
    {
        // MapLabel is "0:a:N" for audio streams. Pull N; fall back to 0 on
        // unexpected shapes so we still produce a unique-per-call suffix.
        int lastColon = mapLabel.LastIndexOf(value: ':');
        return lastColon >= 0 && int.TryParse(s: mapLabel[(lastColon + 1)..], result: out int idx) ? idx : 0;
    }
}
