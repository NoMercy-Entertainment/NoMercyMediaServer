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

namespace NoMercy.Encoder.Subtitles;

public enum SubtitleSourceType
{
    Text,
    Bitmap,
}

/// <summary>
/// What the encoder should do with a subtitle stream — the cross product of
/// source type, container, and the user-requested mode boils down to one of
/// these actions. The caller still owns HOW to execute the action (which
/// filter, which extractor); the router only owns the decision tree.
/// </summary>
public enum SubtitleAction
{
    /// <summary>Stream-copy the subtitle inside the same container (mkv → mkv).</summary>
    Copy,

    /// <summary>Convert text-based subtitle into HLS-segmented WebVTT.</summary>
    ExtractVtt,

    /// <summary>Write a stand-alone .vtt next to the output (mp4/dash sidecar).</summary>
    ExtractVttSidecar,

    /// <summary>MP4 native text-subtitle track (codec mov_text).</summary>
    MovText,

    /// <summary>OCR the bitmap subtitle into a per-segment .vtt playlist.</summary>
    Ocr,

    /// <summary>OCR the bitmap subtitle into a stand-alone sidecar .vtt.</summary>
    OcrSidecar,

    /// <summary>Burn the subtitle directly into the video stream.</summary>
    BurnIn,

    /// <summary>Container does not support subtitles — skip.</summary>
    None,
}

public sealed record SubtitleRouting(SubtitleAction Action, string? Reason = null);

public interface ISubtitleRouter
{
    SubtitleRouting Resolve(
        SubtitleSourceType source,
        OutputFormat container,
        SubtitlePolicy policy
    );
}

/// <summary>
/// Single source of truth for "given a (source, container, mode), what action
/// does the pipeline take on this subtitle?". Replaces scattered switches in
/// <c>StreamActionResolver</c>, <c>SubtitleExtractor</c>, and the burn-in
/// branch in <c>BuildStage</c>. Callers can swap the implementation in DI to
/// experiment with different routings without touching the consumers.
/// </summary>
public sealed class SubtitleRouter : ISubtitleRouter
{
    public SubtitleRouting Resolve(
        SubtitleSourceType source,
        OutputFormat container,
        SubtitlePolicy policy
    )
    {
        // Audio-only containers carry no subtitles by definition.
        if (container is OutputFormat.Mp3 or OutputFormat.Flac or OutputFormat.Ogg)
            return new(Action: SubtitleAction.None, Reason: "container has no subtitle support");

        // Omit — caller explicitly wants no subtitle output.
        if (policy == SubtitlePolicy.Omit)
            return new(Action: SubtitleAction.None, Reason: "subtitle omitted by policy");

        // BurnIn always wins — caller picked it explicitly.
        if (policy == SubtitlePolicy.BurnIn)
            return new(Action: SubtitleAction.BurnIn);

        return (source, container) switch
        {
            (SubtitleSourceType.Text, OutputFormat.Mkv) => new(Action: SubtitleAction.Copy),
            (SubtitleSourceType.Text, OutputFormat.Hls) => new(Action: SubtitleAction.ExtractVtt),
            // text+mp4+Extract → MovText (mov_text is the native MP4 text track).
            // Copy explicitly skips the embed and writes a .vtt sidecar
            // so external player UIs can pick it up without parsing the moov.
            (SubtitleSourceType.Text, OutputFormat.Mp4) => policy switch
            {
                SubtitlePolicy.Copy => new(Action: SubtitleAction.ExtractVttSidecar),
                _ => new(Action: SubtitleAction.MovText),
            },
            (SubtitleSourceType.Text, OutputFormat.Dash) => new(Action: SubtitleAction.ExtractVttSidecar),

            (SubtitleSourceType.Bitmap, OutputFormat.Mkv) => new(Action: SubtitleAction.Copy),
            (SubtitleSourceType.Bitmap, OutputFormat.Hls) => new(Action: SubtitleAction.Ocr),
            (SubtitleSourceType.Bitmap, OutputFormat.Mp4) => new(Action: SubtitleAction.OcrSidecar),
            (SubtitleSourceType.Bitmap, OutputFormat.Dash) => new(Action: SubtitleAction.OcrSidecar),

            _ => new(Action: SubtitleAction.None, Reason: $"no routing for {source} → {container} ({policy})"),
        };
    }
}
