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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Two-pass FFmpeg command assembly extracted from BuildStage: pass-2 flag
/// injection, per-variant stats-file path resolution, and pass-1 command
/// construction.
/// </summary>
public static class TwoPassCommandBuilder
{
    /// <summary>
    /// Returns a copy of <paramref name="plan"/> with <c>-pass 2</c> +
    /// <c>-passlogfile</c> injected into every video output's extra flags,
    /// so the shared output strategy emits them on the FFmpeg command.
    /// Each variant gets its own stats file (keyed on index) — the strategy
    /// generates the matching set in pass 1.
    /// </summary>
    public static OutputPlan InjectPass2Flags(OutputPlan plan, string statsFilePath)
    {
        VideoOutputPlan[] updated = plan
            .VideoOutputs.Select(
                (v, index) =>
                {
                    Dictionary<string, string> flags = new(v.ExtraFlags)
                    {
                        ["-pass"] = "2",
                        ["-passlogfile"] = VariantStatsPath(statsFilePath, index),
                    };
                    return v with { ExtraFlags = flags };
                }
            )
            .ToArray();

        return plan with
        {
            VideoOutputs = updated,
        };
    }

    /// <summary>
    /// Per-variant stats path — each variant writes its own <c>-0.log</c>
    /// and <c>-0.log.mbtree</c> so measurements stay independent. Appending
    /// <c>_v{index}</c> to the base path keeps them colocated.
    /// </summary>
    internal static string VariantStatsPath(string basePath, int variantIndex) =>
        $"{basePath}_v{variantIndex}";

    /// <summary>
    /// Builds the pass-1 FFmpeg command: video-only analysis that writes its
    /// stats to <paramref name="statsFilePath"/> and discards actual output.
    /// <paramref name="variantIndex"/> picks which variant to analyze — the
    /// strategy loops 0..N-1 for multi-variant profiles.
    /// </summary>
    public static FfmpegCommand BuildPass1Command(
        OutputPlan plan,
        MediaInfo? mediaInfo,
        string inputPath,
        string outputDirectory,
        string statsFilePath,
        string ffmpegPath,
        int variantIndex = 0
    )
    {
        VideoOutputPlan video = plan.VideoOutputs[variantIndex];

        FfmpegCommandBuilder builder = new();
        builder.AddInput(new(inputPath));

        // Pass 1 analyzes the single target variant — strip the other variants,
        // audio, subtitles, and thumbnails so the filter graph only produces
        // the one video label. Much cheaper than decoding + filtering 4 variants
        // when only one is being measured.
        OutputPlan videoOnly = plan with
        {
            VideoOutputs = [video],
            AudioOutputs = [],
            SubtitleOutputs = [],
            Thumbnails = null,
        };
        // Pass 1 never burns subtitles — no builder needed.
        string? filterGraph = FilterGraphAssembler.BuildFilterGraph(
            videoOnly,
            mediaInfo,
            inputPath,
            assBurnInFilterBuilder: null
        );
        if (filterGraph is not null)
            builder.WithFilterComplex(filterGraph);

        // Pass 1 output: video encoder settings + -pass 1 + null sink.
        Dictionary<string, string> extraFlags = new(video.ExtraFlags)
        {
            ["-pass"] = "1",
            ["-passlogfile"] = statsFilePath,
            ["-an"] = string.Empty,
            ["-sn"] = string.Empty,
            ["-f"] = "null",
        };

        builder.AddOutput(
            new(
                FilePath: "-",
                VideoCodec: video.EncoderName,
                VideoBitrateKbps: video.BitrateKbps > 0 ? video.BitrateKbps : null,
                Preset: video.Preset,
                Profile: video.Profile,
                Level: video.Level,
                PixelFormat: video.TenBit ? video.PixelFormat : null,
                MapStreams: [video.MapLabel],
                ExtraFlags: extraFlags
            )
        );

        return builder.Build(ffmpegPath, outputDirectory);
    }
}
