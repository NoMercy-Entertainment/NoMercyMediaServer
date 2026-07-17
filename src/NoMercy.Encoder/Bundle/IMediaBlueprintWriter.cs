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
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Writes the single per-media-item <c>.nomercy.json</c> blueprint — replaces
/// the old per-preset <c>manifest.json</c> + <c>reconstruction.json</c> pair.
/// See <c>.claude/specs/reconstruction-blueprint/SPEC.md</c>.
/// </summary>
public interface IMediaBlueprintWriter
{
    /// <summary>
    /// Writes or merges this preset's <see cref="BlueprintEncode"/> entry into
    /// <paramref name="mediaRootPath"/>/<see cref="MediaBlueprintWriter.FileName"/>.
    /// When the file already exists its <c>identity</c> and <c>source</c>
    /// blocks are preserved and this preset's entry in <c>encodes[]</c> is
    /// appended (or replaced, matched on <c>preset_slug</c>) — a re-encode
    /// with a new preset never drops a sibling preset's history.
    /// </summary>
    Task WriteAsync(
        IStorage storage,
        string mediaRootPath,
        MediaInfo source,
        BlueprintIdentity identity,
        OutputPlan plan,
        BundleLayout layout,
        IReadOnlyList<string> outputFiles,
        string outputLocation,
        string encoderVersion,
        string? profileFingerprint,
        DateTime createdAt,
        DateTime completedAt,
        CancellationToken ct
    );

    /// <summary>
    /// Reads a previously written blueprint. Returns null when
    /// <paramref name="mediaRootPath"/>/<see cref="MediaBlueprintWriter.FileName"/>
    /// does not exist.
    /// </summary>
    Task<MediaBlueprint?> ReadAsync(IStorage storage, string mediaRootPath, CancellationToken ct);
}
