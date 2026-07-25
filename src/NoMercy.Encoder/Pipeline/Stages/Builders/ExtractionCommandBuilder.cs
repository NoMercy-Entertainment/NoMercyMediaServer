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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Builds ONE ffmpeg command that covers both font-attachment dumping and
/// bitmap-subtitle copying for a single input. Sources typically live on
/// remote storage (NFS/SMB/S3), so every extra "-i" re-reads the whole
/// multi-GB file over the network — merging what used to be N+1 separate
/// commands (one per bitmap subtitle stream, plus one for font attachments)
/// into a single pass turns N+1 network reads into exactly one.
///
/// Kept as its own dedicated command (never folded into the main encode
/// command): the main encode can run for hours, and an extraction failure
/// here must never sink an otherwise-successful encode.
/// </summary>
public static class ExtractionCommandBuilder
{
    /// <summary>
    /// Returns null when there is nothing to extract (no attachments, no
    /// bitmap subtitles) — callers must not spawn a pointless ffmpeg process.
    /// </summary>
    public static FfmpegCommand? BuildCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        OutputPlan plan,
        MediaInfo mediaInfo,
        string mediaTitle,
        ISubtitleExtractor subtitleExtractor,
        IFontExtractor fontExtractor,
        IStorage storage,
        IReadOnlyList<AttachmentInfo> attachments
    )
    {
        List<string> args = ["-y", "-hide_banner"];

        if (attachments.Count > 0)
            AddAttachmentDumpArgs(args, outputDirectory, attachments, fontExtractor, storage);

        List<(string OutputPath, int SourceIndex)> bitmapOutputs = ResolveBitmapSubtitleOutputs(
            plan,
            mediaInfo,
            outputDirectory,
            mediaTitle,
            subtitleExtractor,
            storage
        );

        if (attachments.Count == 0 && bitmapOutputs.Count == 0)
            return null;

        args.Add("-i");
        args.Add(inputPath);

        if (bitmapOutputs.Count == 0)
        {
            // Attachments only — no real stream output, so ffmpeg needs a
            // sink to write to. Matches the previous font-only extraction.
            args.Add("-f");
            args.Add("null");
            args.Add("-");
        }
        else
        {
            AddBitmapSubtitleOutputArgs(args, bitmapOutputs);
        }

        return new(ffmpegPath, args.ToArray(), outputDirectory);
    }

    // -dump_attachment is a pre-input flag, so these must be added before
    // "-i". Passing an explicit, sanitized output filename per attachment —
    // rather than the bulk -dump_attachment:t "" form, which reuses each
    // attachment's embedded filename — stops a single attachment whose
    // embedded name ffmpeg rejects as "unsafe" from aborting the whole dump.
    // Naming/dedup stays owned by IFontExtractor so a plugin's replacement
    // implementation controls it too.
    private static void AddAttachmentDumpArgs(
        List<string> args,
        string outputDirectory,
        IReadOnlyList<AttachmentInfo> attachments,
        IFontExtractor fontExtractor,
        IStorage storage
    )
    {
        string fontDir = storage.CombinePath(outputDirectory, "fonts");
        storage.CreateDirectory(fontDir);

        foreach (
            AttachmentDumpTarget target in fontExtractor.ResolveAttachmentDumpTargets(attachments)
        )
        {
            args.Add($"-dump_attachment:{target.Index}");
            args.Add(target.RelativePath);
        }
    }

    private static void AddBitmapSubtitleOutputArgs(
        List<string> args,
        List<(string OutputPath, int SourceIndex)> bitmapOutputs
    )
    {
        foreach ((string outputPath, int sourceIndex) in bitmapOutputs)
        {
            args.Add("-map");
            args.Add($"0:s:{sourceIndex}");
            args.Add("-c:s");
            args.Add("copy");
            // Must specify -f matroska explicitly — FFmpeg doesn't auto-detect .mks.
            args.Add("-f");
            args.Add("matroska");
            args.Add(outputPath);
        }
    }

    // Bitmap subs (dvd_subtitle, PGS) can't be muxed to .sub+.idx in a
    // multi-output command. They're extracted as MKS (Matroska subtitle
    // container), which preserves the original format.
    private static List<(string OutputPath, int SourceIndex)> ResolveBitmapSubtitleOutputs(
        OutputPlan plan,
        MediaInfo mediaInfo,
        string outputDirectory,
        string mediaTitle,
        ISubtitleExtractor subtitleExtractor,
        IStorage storage
    )
    {
        List<(string OutputPath, int SourceIndex)> outputs = [];

        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
            if (subPlan.Policy == SubtitlePolicy.BurnIn)
                continue;

            if (subPlan.Action is not (StreamAction.Extract or StreamAction.Copy))
                continue;

            if (subPlan.SourceIndex >= mediaInfo.SubtitleStreams.Count)
                continue;

            SubtitleStreamInfo stream = mediaInfo.SubtitleStreams[subPlan.SourceIndex];

            // Text subtitles are muxed into the main command via
            // AddTextSubtitleOutputs — only bitmap subs are handled here.
            if (stream.IsTextBased)
                continue;

            SubtitleOutputInfo info = subtitleExtractor.ResolveOutput(
                subPlan,
                stream,
                outputDirectory,
                mediaTitle
            );

            // Ensure subtitle directory exists (storage-relative parent of OutputPath).
            string? parentDir = storage.GetParent(info.OutputPath);
            if (parentDir is not null)
                storage.CreateDirectory(storage.CombinePath(outputDirectory, parentDir));

            // Use MKS (Matroska) container for bitmap subs.
            string outputPath = Path.ChangeExtension(info.OutputPath, ".mks");

            outputs.Add((outputPath, info.SourceIndex));
        }

        return outputs;
    }
}
