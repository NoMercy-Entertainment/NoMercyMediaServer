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
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Builds a <see cref="MediaBlueprint"/> from a source analysis and, once a
/// preset run completes, one <see cref="BlueprintEncode"/> entry per
/// encode. Pure function throughout — no DB lookups, no I/O.
/// </summary>
public interface IMediaBlueprintBuilder
{
    /// <summary>
    /// Build the identity + source blocks from <paramref name="source"/> with
    /// an empty <see cref="MediaBlueprint.Encodes"/> list. Callers append
    /// encode entries once a preset run completes.
    /// </summary>
    MediaBlueprint BuildFromSource(
        MediaInfo source,
        BlueprintIdentity identity,
        string? originalSourcePath = null
    );

    /// <summary>
    /// Builds one <see cref="BlueprintEncode"/> entry for a completed preset
    /// run. Walks every <c>source.VideoStreams</c> / <c>AudioStreams</c> /
    /// <c>SubtitleStreams</c> entry — never the plan's outputs — so a stream
    /// that produced no artifact still lands as <c>policy: "dropped"</c>
    /// instead of silently vanishing from the record. See spec "Tracks and
    /// artifact selection" and "The invariant".
    /// </summary>
    /// <param name="source">The analysed source — the stream list this walks.</param>
    /// <param name="plan">The resolved output plan for this preset run.</param>
    /// <param name="layout">Resolved bundle layout (preset id/slug/name, container).</param>
    /// <param name="outputFiles">
    /// The real on-disk file list for this encode, relative to the media
    /// item's own folder root — the same listing FinalizeStage already has.
    /// </param>
    /// <param name="outputLocation">The media item's own folder (the real destination).</param>
    /// <param name="encoderVersion">The running encoder's assembly version.</param>
    /// <param name="profileFingerprint">
    /// The resolved profile's fingerprint — what the reconciler reads back.
    /// Null for callers that predate reconciliation.
    /// </param>
    /// <param name="createdAt">When this preset run started.</param>
    /// <param name="completedAt">When this preset run finished.</param>
    BlueprintEncode BuildEncode(
        MediaInfo source,
        OutputPlan plan,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles,
        string outputLocation,
        string encoderVersion,
        string? profileFingerprint,
        DateTime createdAt,
        DateTime completedAt
    );
}
