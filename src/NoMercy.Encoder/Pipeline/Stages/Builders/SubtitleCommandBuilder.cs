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
/// FFmpeg subtitle output assembly extracted from BuildStage: text-subtitle
/// muxing into the main command. Bitmap-subtitle extraction now lives in
/// <see cref="ExtractionCommandBuilder"/>, merged with font-attachment
/// dumping into a single command so remote sources aren't re-read once per
/// stream.
/// </summary>
public static class SubtitleCommandBuilder
{
    /// <summary>
    /// Adds text subtitle outputs (WebVTT, ASS) to the main FFmpeg command builder.
    /// Bitmap subtitles are handled separately via <see cref="ExtractionCommandBuilder"/>.
    /// </summary>
    public static void AddTextSubtitleOutputs(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        MediaInfo mediaInfo,
        string outputDirectory,
        string mediaTitle,
        ISubtitleExtractor subtitleExtractor,
        IStorage storage
    )
    {
        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
            if (subPlan.Policy == SubtitlePolicy.BurnIn)
                continue;

            if (subPlan.Action is not (StreamAction.Extract or StreamAction.Copy))
                continue;

            if (subPlan.SourceIndex >= mediaInfo.SubtitleStreams.Count)
                continue;

            SubtitleStreamInfo stream = mediaInfo.SubtitleStreams[subPlan.SourceIndex];

            // Only text subtitles in the main command
            if (!stream.IsTextBased)
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

            // FFmpeg gets the relative path (CWD = output directory)
            builder.AddOutput(
                new(
                    info.OutputPath,
                    SubtitleCodec: info.FfmpegCodec,
                    MapStreams: [$"0:s:{info.SourceIndex}"]
                )
            );
        }
    }
}
