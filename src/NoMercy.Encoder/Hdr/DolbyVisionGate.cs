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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Hdr;

/// <summary>
/// Result of evaluating whether a Dolby Vision RPU can survive into the output.
/// ExtraFlags is empty when DV is stripped; populated with container-specific
/// metadata flags when DV is preserved.
/// </summary>
public sealed record DolbyVisionDecision(
    bool Preserved,
    string Reason,
    Dictionary<string, string> ExtraFlags
);

/// <summary>
/// Pure static gate — no DI, no side effects other than writing to the
/// decision log. Called by PlanStage immediately after bit-depth resolution.
///
/// Four conditions must ALL pass for DV to survive into the output:
///   1. Source carries Dolby Vision metadata (HasRpu).
///   2. Output codec is HEVC or AV1.
///   3. Output bit-depth >= 10.
///   4. Container is HLS, MP4, or MKV.
///   + HdrPolicy must not be AlwaysTonemap (forces strip even when all four pass).
///
/// AlwaysPreserve overrides nothing — it only means "don't tonemap"; if the
/// codec or container can't carry DV, the RPU is still stripped. The gate logs
/// the reason either way so the dashboard can surface it.
/// </summary>
public static class DolbyVisionGate
{
    private static readonly HashSet<VideoCodecType> DvCompatibleCodecs = new()
    {
        VideoCodecType.H265,
        VideoCodecType.Av1,
    };

    private static readonly HashSet<OutputFormat> DvCompatibleContainers = new()
    {
        OutputFormat.Hls,
        OutputFormat.Mp4,
        OutputFormat.Mkv,
    };

    /// <summary>
    /// Evaluate the four DV preservation conditions and return a decision.
    /// When <paramref name="source"/> is null the method returns immediately
    /// with Preserved=false and logs nothing (no DV to preserve).
    /// </summary>
    public static DolbyVisionDecision Resolve(
        DolbyVisionInfo? source,
        VideoCodecType outputCodec,
        int outputBitDepth,
        OutputFormat container,
        HdrPolicies policies,
        bool videoIsStreamCopy,
        IDecisionLogSink decisions,
        bool hlsUsesFmp4Segments = true
    )
    {
        // --- Condition 0: no DV in source → silent no-op ---------------------
        if (source is null)
            return new(
                Preserved: false,
                Reason: "source has no Dolby Vision metadata",
                ExtraFlags: new()
            );

        // --- Condition: AlwaysTonemap forces strip ----------------------------
        if (policies == HdrPolicies.AlwaysTonemap)
        {
            string reason = "hdr_policy = AlwaysTonemap forces tonemap to SDR";
            decisions.Add(
                entry: new(Stage: "plan", Key: "plan.dv_stripped", Message: reason, Data: new { policy = "AlwaysTonemap" })
            );
            return new(Preserved: false, Reason: reason, ExtraFlags: new());
        }

        // --- Condition: RPU survives only a bitstream copy --------------------
        // The Dolby Vision RPU lives inside the HEVC bitstream as SEI NAL units.
        // A stream copy passes them through untouched; ANY re-encode strips them
        // — nvenc carries no RPU at all, and every crop / scale / tonemap forces
        // a re-encode. Tagging a re-encoded stream dvh1 yields a file that claims
        // Dolby Vision but has no RPU and no DV configuration box: non-conformant,
        // and players that honour the tag fail to decode it (the reported corrupt
        // playback). Only a copied stream may keep the dvh1 tag.
        if (!videoIsStreamCopy)
        {
            string reason = "output is re-encoded — Dolby Vision RPU survives only a stream copy";
            decisions.Add(entry: new(Stage: "plan", Key: "plan.dv_stripped", Message: reason, Data: new { reEncoded = true }));
            return new(Preserved: false, Reason: reason, ExtraFlags: new());
        }

        // --- Condition 1: codec must be HEVC or AV1 --------------------------
        if (!DvCompatibleCodecs.Contains(item: outputCodec))
        {
            string reason =
                $"output codec '{outputCodec}' does not support Dolby Vision (HEVC or AV1 required)";
            decisions.Add(
                entry: new(Stage: "plan", Key: "plan.dv_stripped", Message: reason, Data: new { codec = outputCodec.ToString() })
            );
            return new(Preserved: false, Reason: reason, ExtraFlags: new());
        }

        // --- Condition 2: bit-depth >= 10 ------------------------------------
        if (outputBitDepth < 10)
        {
            string reason = $"output bit_depth {outputBitDepth} below 10 — DV requires 10-bit";
            decisions.Add(
                entry: new(Stage: "plan", Key: "plan.dv_stripped", Message: reason, Data: new { bitDepth = outputBitDepth })
            );
            return new(Preserved: false, Reason: reason, ExtraFlags: new());
        }

        // --- Condition 3: container must support DV RPU ----------------------
        if (!DvCompatibleContainers.Contains(item: container))
        {
            string reason = $"container '{container}' does not carry DV RPU";
            decisions.Add(
                entry: new(Stage: "plan", Key: "plan.dv_stripped", Message: reason, Data: new { container = container.ToString() })
            );
            return new(Preserved: false, Reason: reason, ExtraFlags: new());
        }

        // --- All four conditions pass — build container-specific extra flags --
        Dictionary<string, string> extraFlags = BuildExtraFlags(
            source: source,
            container: container,
            hlsUsesFmp4Segments: hlsUsesFmp4Segments,
            decisions: decisions
        );

        // HLS + mpegts is a hard blocker — the gate strips rather than silently
        // overriding the user's explicit segment type choice.
        if (extraFlags.Count == 0 && container == OutputFormat.Hls)
        {
            string reason =
                "HLS mpegts segments do not carry DV RPU — use Container.HlsFmp4 to preserve DV";
            decisions.Add(
                entry: new(
                    Stage: "plan",
                    Key: "plan.dv_stripped",
                    Message: reason,
                    Data: new { container = "Hls", segmentType = "mpegts" }
                )
            );
            return new(Preserved: false, Reason: reason, ExtraFlags: new());
        }

        string preserveReason = $"DV profile {source.Profile} level {source.Level} preserved";
        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.dv_preserved",
                Message: preserveReason,
                Data: new
                {
                    dvProfile = source.Profile,
                    dvLevel = source.Level,
                    hasRpu = source.HasRpu,
                    hasEl = source.HasEl,
                    container = container.ToString(),
                    extraFlags,
                }
            )
        );

        return new(Preserved: true, Reason: preserveReason, ExtraFlags: extraFlags);
    }

    private static Dictionary<string, string> BuildExtraFlags(
        DolbyVisionInfo source,
        OutputFormat container,
        bool hlsUsesFmp4Segments,
        IDecisionLogSink decisions
    )
    {
        Dictionary<string, string> flags = new();

        switch (container)
        {
            case OutputFormat.Mp4:
                // MP4 requires the dvh1 codec tag to signal DV-compatible MP4 to players.
                // iso6 brand marks the file as ISO Base Media File Format v6 which DV demands.
                flags[key: "-tag:v"] = "dvh1";
                flags[key: "-brand"] = "iso6";
                break;

            case OutputFormat.Mkv:
                // MKV carries the DV RPU natively in the HEVC/AV1 bitstream.
                // The default disposition is fine — just mark the video track as default.
                flags[key: "-disposition:v"] = "default";
                break;

            case OutputFormat.Hls:
                // HLS DV requires fmp4 segments. If the caller's container is HlsTs
                // (mpegts) we don't silently override — return empty flags to trigger
                // the strip path so the operator sees the warning.
                if (!hlsUsesFmp4Segments)
                    return new();

                flags[key: "-hls_segment_type"] = "fmp4";
                break;
        }

        return flags;
    }
}
