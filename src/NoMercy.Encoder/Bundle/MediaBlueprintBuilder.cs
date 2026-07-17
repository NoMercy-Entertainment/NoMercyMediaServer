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

namespace NoMercy.Encoder.Bundle;

/// <inheritdoc cref="IMediaBlueprintBuilder"/>
public class MediaBlueprintBuilder : IMediaBlueprintBuilder
{
    private const int CurrentVersion = 1;

    public MediaBlueprint BuildFromSource(MediaInfo source, BlueprintIdentity identity)
    {
        BlueprintSource blueprintSource = new(
            Path: source.FilePath,
            Filename: Path.GetFileName(source.FilePath),
            Container: source.Format,
            SizeBytes: source.FileSizeBytes,
            DurationSeconds: source.Duration.TotalSeconds,
            // Deferred until a streaming hasher is wired into the analyzer —
            // see spec "Open items".
            Sha256: null,
            Ffprobe: source.Ffprobe
        );

        return new(
            Version: CurrentVersion,
            Identity: identity,
            Source: blueprintSource,
            // Zero encode entries: this builder runs source-derived only, no
            // encode has happened yet. Proves the blueprint is generatable
            // with zero encode outputs — the foundation-slice invariant.
            Encodes: []
        );
    }
}
