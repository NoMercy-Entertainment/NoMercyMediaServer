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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// AudioOutputPlan[] assembly extracted from PlanStage.BuildOutputPlanAsync.
/// Pure projection over the profile + probed media; no encoder state.
/// </summary>
public static class AudioPlanBuilder
{
    public static AudioOutputPlan[] Build(EncodingProfile profile, MediaInfo media)
    {
        List<AudioOutputPlan> audioPlans = [];
        foreach (AudioOutput audioProfile in profile.Audio)
        {
            string encoderName = AudioCodecDefinitions.GetEncoder(audioProfile.Codec).FfmpegName;
            HashSet<string> allowed =
                audioProfile.AllowedLanguages.Length > 0
                    ? new HashSet<string>(
                        audioProfile.AllowedLanguages,
                        StringComparer.OrdinalIgnoreCase
                    )
                    : [];

            for (int si = 0; si < media.AudioStreams.Count; si++)
            {
                AudioStreamInfo stream = media.AudioStreams[si];
                string streamLang = stream.Language ?? "und";

                if (allowed.Count > 0 && !allowed.Contains(streamLang))
                    continue;

                LoudnessMode loudnessMode = audioProfile.Loudness?.Mode ?? LoudnessMode.None;
                DownmixMode downmixMode = audioProfile.Downmix?.Mode ?? DownmixMode.Auto;
                string? customPanMatrix = audioProfile.Downmix?.CustomPanMatrix;

                string? audioFilter = AudioFilterBuilder.BuildAudioFilter(
                    loudnessMode,
                    downmixMode,
                    customPanMatrix
                );

                audioPlans.Add(
                    new(
                        EncoderName: encoderName,
                        BitrateKbps: audioProfile.BitrateKbps,
                        Channels: audioProfile.Channels,
                        SampleRate: audioProfile.SampleRateHz,
                        Action: StreamAction.Transcode,
                        Language: streamLang,
                        MapLabel: $"0:a:{si}",
                        SegmentNameTemplate: audioProfile.SegmentNameTemplate,
                        PlaylistNameTemplate: audioProfile.PlaylistNameTemplate,
                        AudioFilter: audioFilter,
                        ExtraFlags: audioProfile.CustomArguments is not null
                            ? new Dictionary<string, string>(audioProfile.CustomArguments)
                            : null
                    )
                );
            }
        }

        // Disambiguate any audio plans whose templates would resolve to the
        // same on-disk path. The default audio template is
        // "audio_{lang}_{codec}/audio_{lang}_{codec}", so three English AAC
        // streams all collapse to the same directory + filename. Append the
        // source stream index to colliding plans only — single-stream-per-
        // language sources keep their stable templates.
        return PlanStageDisambiguation.DisambiguateAudio(audioPlans).ToArray();
    }
}
