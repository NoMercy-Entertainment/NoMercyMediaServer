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

using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Projects an <see cref="ExecutionPlan"/> (internal execution graph) into
/// a <see cref="PlanResult"/> (stable dashboard spec-shape). The two types
/// evolve on different schedules — this class owns the mapping so neither
/// side leaks into the other.
///
/// <para>Fields not yet populated by <see cref="ExecutionPlan"/> (e.g.
/// <c>EncoderHandle</c>, <c>GpuIndex</c>, <c>FilterChain</c>) default to
/// empty / null today. Phase 3 will enrich <see cref="ExecutionPlan"/> with
/// hardware-binding information and update the mapping here.</para>
/// </summary>
public class PlanResultProjector : IPlanResultProjector
{
    /// <summary>
    /// Build a <see cref="PlanResult"/> from the plan produced by
    /// <see cref="PlanStage"/> and the current <see cref="EncodingContext"/>.
    /// </summary>
    public PlanResult FromExecutionPlan(ExecutionPlan plan, EncodingContext context)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        OutputPlan output = plan.OutputPlan;
        string strategy = BuildStrategyLabel(output);

        List<VariantPlan> variants = BuildVariants(output);
        List<SubtitlePlan> subtitles = BuildSubtitles(output);
        HardwareBindings hardware = new(null, null);
        IReadOnlyList<DecisionLog> decisions = context.DecisionsOrNoOp.Snapshot();

        return new(strategy, variants, subtitles, hardware, decisions);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static string BuildStrategyLabel(OutputPlan output)
    {
        string format = output.Format.ToString();

        if (output.VideoOutputs.Length == 0)
            return $"{format} · audio-only";

        VideoOutputPlan first = output.VideoOutputs[0];
        string rateLabel = first.Crf > 0 ? $"CRF {first.Crf}" : $"{first.BitrateKbps} kbps";
        return $"{format} · {rateLabel}";
    }

    private static List<VariantPlan> BuildVariants(OutputPlan output)
    {
        List<VariantPlan> variants = [];

        for (int i = 0; i < output.VideoOutputs.Length; i++)
        {
            VideoOutputPlan v = output.VideoOutputs[i];
            string variantId = $"v{i}_{v.Width}x{v.Height}";

            RateControl rateControl = ResolveRateControl(v);

            VideoTarget video = new(
                v.EncoderName,
                v.EncoderName,
                null,
                v.Width,
                v.Height,
                rateControl,
                v.Preset ?? string.Empty,
                v.Profile ?? string.Empty,
                v.Level ?? string.Empty,
                v.PixelFormat,
                []
            );

            // Associate audio outputs that share this variant index by round-robin.
            // Phase 3 will carry explicit variant→audio grouping from ExecutionPlan.
            // For now, all audio tracks are listed on every variant — this is what
            // the dashboard needs to show the stream list before encoding starts.
            List<AudioTarget> audio = [];
            for (int ai = 0; ai < output.AudioOutputs.Length; ai++)
            {
                AudioOutputPlan a = output.AudioOutputs[ai];
                audio.Add(
                    new(
                        ai,
                        a.EncoderName,
                        a.Channels,
                        a.BitrateKbps,
                        a.Language
                    )
                );
            }

            variants.Add(
                new(
                    variantId,
                    video,
                    audio,
                    output.SegmentDurationSeconds,
                    output.SegmentDurationSeconds
                )
            );
        }

        return variants;
    }

    private static RateControl ResolveRateControl(VideoOutputPlan v)
    {
        bool hasCrf = v.Crf > 0;
        bool hasBitrate = v.BitrateKbps > 0;

        if (hasCrf && hasBitrate)
        {
            // CRF quality target with a hard bitrate ceiling.
            int maxKbps = v.BitrateKbps;
            int bufKbps = maxKbps * 2;
            return new RateControl.CrfCapped(v.Crf, maxKbps, bufKbps);
        }

        if (hasCrf)
            return new RateControl.Crf(v.Crf);

        // Pure bitrate: use 1.2× as max and 2× as buffer (reasonable defaults
        // until Phase 3 populates explicit max/buf from the profile).
        int abr = v.BitrateKbps;
        return new RateControl.Abr(abr, (int)(abr * 1.2), abr * 2);
    }

    private static List<SubtitlePlan> BuildSubtitles(OutputPlan output)
    {
        List<SubtitlePlan> subtitles = [];

        foreach (SubtitleOutputPlan s in output.SubtitleOutputs)
        {
            string action = s.Action switch
            {
                StreamAction.Extract when s.Policy == SubtitlePolicy.BurnIn => "burn_in",
                StreamAction.Extract => "extract",
                StreamAction.Transcode => "burn_in",
                _ => "copy",
            };

            subtitles.Add(
                new(
                    s.SourceIndex,
                    s.OutputCodec.ToString(),
                    s.Language,
                    action
                )
            );
        }

        return subtitles;
    }
}
