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

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Metadata-injection command assembly extracted from BuildStage: splices
/// -metadata / per-stream / disposition args into the built command and
/// resolves the track list (applying MetadataMerger precedence in copy mode).
/// </summary>
public static class MetadataInjectionBuilder
{
    /// <summary>
    /// Splices -metadata / per-stream metadata / disposition / attachment
    /// args from <paramref name="mediaItem"/> into <paramref name="command"/>
    /// just before the last argument (output filename). When no injector is
    /// configured or the media item is null, the command is returned unchanged.
    ///
    /// For copy-mode encodes (<paramref name="isCopyMode"/> true), MetadataMerger
    /// is called first to apply field-level source-vs-DB precedence rules.
    /// For transcode encodes source metadata is discarded and DB tracks are used
    /// directly (streams are re-encoded from scratch, so source tags don't survive).
    /// </summary>
    public static FfmpegCommand InjectMetadataArgs(
        IMetadataInjector? metadataInjector,
        IMetadataMerger? metadataMerger,
        FfmpegCommand command,
        MediaItemRef? mediaItem,
        EncodingContext context,
        bool isCopyMode
    )
    {
        if (metadataInjector is null || mediaItem is null)
            return command;

        IReadOnlyList<TrackMetadata> tracks = ResolveTracksForInjection(
            metadataMerger,
            context,
            isCopyMode
        );

        MetadataInjectionContext ctx = new(Media: mediaItem, Tracks: tracks, AttachmentPaths: []);

        IReadOnlyList<string> metaArgs = metadataInjector.BuildArgs(ctx);
        if (metaArgs.Count == 0)
            return command;

        // Insert the metadata flags before the last argument (output filepath).
        string[] original = command.Arguments;
        string[] updated = new string[original.Length + metaArgs.Count];
        int insertAt = original.Length - 1;
        Array.Copy(original, updated, insertAt);
        for (int i = 0; i < metaArgs.Count; i++)
            updated[insertAt + i] = metaArgs[i];
        updated[^1] = original[^1];

        return command with
        {
            Arguments = updated,
        };
    }

    /// <summary>
    /// Resolves the track list to pass to MetadataInjector.
    /// When <paramref name="isCopyMode"/> is true and both SourceTracks and
    /// DbTracks are present on the context, MetadataMerger applies field-level
    /// precedence rules. Otherwise DB tracks (or an empty list) are used.
    /// </summary>
    public static IReadOnlyList<TrackMetadata> ResolveTracksForInjection(
        IMetadataMerger? metadataMerger,
        EncodingContext context,
        bool isCopyMode
    )
    {
        IReadOnlyList<TrackMetadata> dbTracks = context.DbTracks ?? [];

        if (
            !isCopyMode
            || metadataMerger is null
            || context.SourceTracks is null
            || context.SourceTracks.Count == 0
        )
            return dbTracks;

        return metadataMerger.Merge(context.SourceTracks, dbTracks);
    }
}
