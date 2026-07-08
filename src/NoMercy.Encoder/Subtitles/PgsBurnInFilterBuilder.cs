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

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// The filter chain produced by <see cref="PgsBurnInFilterBuilder"/>.
/// </summary>
/// <param name="FilterComplex">
/// The <c>-filter_complex</c> value, e.g.
/// <c>[0:v:0][0:s:1]overlay=format=auto[burned]</c>.
/// </param>
/// <param name="MapLabel">
/// The output pad label the caller should pass to <c>-map</c>, e.g.
/// <c>[burned]</c>. Using this label in place of the original video
/// stream selector routes the composited output to the encoder.
/// </param>
/// <param name="VideoLabels">
/// One distinct output pad per video output (rung). A filtergraph output
/// pad can feed exactly one consumer, so with more than one video output the
/// overlay result is <c>split</c> into one pad per rung — mapping a single
/// pad twice makes ffmpeg abort. For a single video output this is just
/// <c>[<see cref="MapLabel"/>]</c>.
/// </param>
/// <param name="ThumbnailLabel">
/// The distinct output pad the thumbnail/sprite branch should read from, or
/// null when no thumbnails were requested. Comes off the same <c>split</c>.
/// </param>
public record PgsBurnInFilterChain(
    string FilterComplex,
    string MapLabel,
    IReadOnlyList<string> VideoLabels,
    string? ThumbnailLabel
);

/// <summary>
/// Builds a <c>-filter_complex</c> chain that overlays PGS (bitmap)
/// subtitles onto the video stream using the FFmpeg <c>overlay</c> filter.
///
/// <para>The <c>subtitles</c> filter cannot decode PGS; instead FFmpeg
/// decodes the subtitle stream to raw RGBA bitmaps and the
/// <c>overlay=format=auto</c> filter composites them onto the video frame
/// at the correct timestamp. The resulting pad label replaces the original
/// video stream selector in <c>-map</c>.</para>
///
/// <para>Example output for video index 0, subtitle index 1:</para>
/// <code>[0:v:0][0:s:1]overlay=format=auto[burned]</code>
/// </summary>
public sealed class PgsBurnInFilterBuilder
{
    /// <summary>
    /// Builds the PGS overlay filter chain.
    /// </summary>
    /// <param name="videoStreamIndex">
    /// Index of the video stream within the first input (0-based). Almost
    /// always <c>0</c> for single-video sources.
    /// </param>
    /// <param name="subtitleStreamIndex">
    /// Index of the subtitle stream within the first input (0-based),
    /// matching <see cref="NoMercy.Encoder.Output.SubtitleOutputPlan.SourceIndex"/>.
    /// </param>
    /// <returns>
    /// A <see cref="PgsBurnInFilterChain"/> whose
    /// <see cref="PgsBurnInFilterChain.FilterComplex"/> can be passed
    /// directly to <c>-filter_complex</c> and whose
    /// <see cref="PgsBurnInFilterChain.MapLabel"/> replaces the video
    /// stream in <c>-map</c>.
    /// </returns>
    public PgsBurnInFilterChain Build(int videoStreamIndex, int subtitleStreamIndex) =>
        Build(videoStreamIndex, subtitleStreamIndex, videoOutputCount: 1, includeThumbnails: false);

    /// <summary>
    /// Builds the PGS overlay chain, splitting the composited output into one
    /// distinct pad per consumer (each video rung, plus an optional thumbnail
    /// branch). A single filtergraph output pad can only feed one <c>-map</c>,
    /// so a multi-rung ladder or a thumbnails output would abort ffmpeg if they
    /// all mapped the same <c>[burned]</c> pad.
    /// </summary>
    public PgsBurnInFilterChain Build(
        int videoStreamIndex,
        int subtitleStreamIndex,
        int videoOutputCount,
        bool includeThumbnails
    )
    {
        int consumers = Math.Max(1, videoOutputCount) + (includeThumbnails ? 1 : 0);

        string overlay = $"[0:v:{videoStreamIndex}][0:s:{subtitleStreamIndex}]overlay=format=auto";

        // One consumer and no thumbnails: the overlay pad is mapped once — no
        // split needed, keep the single [burned] label.
        if (consumers == 1)
        {
            return new(
                FilterComplex: $"{overlay}[burned]",
                MapLabel: "[burned]",
                VideoLabels: ["[burned]"],
                ThumbnailLabel: null
            );
        }

        List<string> videoLabels = [];
        for (int i = 0; i < Math.Max(1, videoOutputCount); i++)
            videoLabels.Add($"[burned{i}]");

        string? thumbnailLabel = includeThumbnails ? "[burnedthumbs]" : null;

        List<string> splitPads = [.. videoLabels];
        if (thumbnailLabel is not null)
            splitPads.Add(thumbnailLabel);

        string splitTargets = string.Concat(splitPads);
        string filterComplex = $"{overlay},split={consumers}{splitTargets}";

        return new(
            FilterComplex: filterComplex,
            MapLabel: videoLabels[0],
            VideoLabels: videoLabels,
            ThumbnailLabel: thumbnailLabel
        );
    }
}
