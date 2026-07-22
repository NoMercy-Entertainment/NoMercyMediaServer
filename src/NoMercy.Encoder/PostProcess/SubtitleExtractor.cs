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
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Encoder.PostProcess;

public class SubtitleExtractor : ISubtitleExtractor
{
    private static readonly HashSet<string> AssCodecs = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        "ass",
        "ssa",
    };

    /// <summary>
    /// Resolves the output file path and FFmpeg codec for a subtitle stream.
    /// ASS/SSA subtitles stay as ASS. Other text subtitles convert to WebVTT.
    /// Bitmap subtitles are extracted as-is (VobSub → .sub+.idx, PGS/DVB → .sup).
    /// </summary>
    public SubtitleOutputInfo ResolveOutput(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string outputDirectory,
        string mediaTitle
    )
    {
        string language = stream.Language ?? plan.Language ?? "und";
        // Trust the plan's variant — PlanStage classifies across all source
        // streams with per-language peer context. Re-running ResolveVariant
        // here on a single stream would re-introduce the bug where every
        // un-classified track collapses to "alt" (or worse, to "full" for
        // multiple peers of the same language with no per-language tiebreak).
        string variant = plan.Variant;
        bool isBitmap = SubtitleClassifier.IsBitmapBased(codec: stream.Codec);
        bool isAss = AssCodecs.Contains(item: stream.Codec);

        string extension;
        string ffmpegCodec;

        if (isBitmap)
        {
            // VobSub gets its native .idx/.sub pair via NoMercy ffmpeg's
            // vobsubenc muxer. PGS / DVB stay in .mks for preservation —
            // OCR pass converts them to WebVTT downstream.
            bool isVobSub = stream.Codec.Equals(value: "dvd_subtitle", comparisonType: StringComparison.OrdinalIgnoreCase);
            extension = isVobSub ? "idx" : "mks";
            ffmpegCodec = "copy";
        }
        else if (isAss)
        {
            // ASS/SSA always preserves styling — never lossy-convert to WebVTT.
            (extension, ffmpegCodec) = ("ass", "ass");
        }
        else
        {
            // Copy = preserve source format byte-for-byte (no ext mismatch for
            // SRT). Explicit codecs force conversion.
            (extension, ffmpegCodec) = plan.OutputCodec switch
            {
                SubtitleCodecType.WebVtt => ("vtt", "webvtt"),
                SubtitleCodecType.Ass => ("ass", "ass"),
                SubtitleCodecType.Srt => ("srt", "srt"),
                SubtitleCodecType.Copy => CopyExtensionFor(sourceCodec: stream.Codec),
                _ => ("vtt", "webvtt"),
            };
        }

        Dictionary<string, string> tokens = TemplateResolver.SubtitleTokens(
            language: language,
            variant: variant,
            filename: mediaTitle
        );
        string resolved = TemplateResolver.Resolve(template: plan.PlaylistNameTemplate, values: tokens);
        // Relative path — FFmpeg CWD is set to the output directory.
        string outputPath = $"{resolved}.{extension}";

        return new(
            OutputPath: outputPath,
            FfmpegCodec: ffmpegCodec,
            Extension: extension,
            Language: language,
            Variant: variant,
            IsBitmap: isBitmap,
            SourceIndex: plan.SourceIndex
        );
    }

    /// <summary>
    /// Resolves the URI for the master playlist's subtitle group entry.
    /// Path is relative to the output directory.
    /// </summary>
    public string ResolvePlaylistUri(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string mediaTitle
    )
    {
        string language = stream.Language ?? plan.Language ?? "und";
        string variant = plan.Variant;
        bool isBitmap = SubtitleClassifier.IsBitmapBased(codec: stream.Codec);
        bool isAss = AssCodecs.Contains(item: stream.Codec);

        string extension;
        if (isBitmap)
        {
            extension = "mks";
        }
        else if (isAss)
        {
            extension = "ass";
        }
        else
        {
            extension = "vtt";
        }

        Dictionary<string, string> tokens = TemplateResolver.SubtitleTokens(
            language: language,
            variant: variant,
            filename: mediaTitle
        );
        string resolved = TemplateResolver.Resolve(template: plan.PlaylistNameTemplate, values: tokens);
        return $"{resolved}.{extension}";
    }

    // Map a source codec to the extension ffmpeg's `-c:s copy` will produce
    // without re-muxing. Falls through to a forced WebVTT conversion only
    // when the source codec is unknown to us — better than writing the
    // wrong extension and confusing the player.
    private static (string extension, string ffmpegCodec) CopyExtensionFor(string sourceCodec) =>
        sourceCodec.ToLowerInvariant() switch
        {
            "ass" or "ssa" => ("ass", "copy"),
            "srt" or "subrip" => ("srt", "copy"),
            "webvtt" => ("vtt", "copy"),
            "mov_text" => ("vtt", "webvtt"),
            _ => ("vtt", "webvtt"),
        };
}

public record SubtitleOutputInfo(
    string OutputPath,
    string FfmpegCodec,
    string Extension,
    string Language,
    string Variant,
    bool IsBitmap,
    int SourceIndex
);
