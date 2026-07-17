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

/// <summary>
/// Builds a <see cref="MediaBlueprint"/> directly from a source analysis —
/// no DB lookups, no encode outputs. Pure function. This is the foundation
/// slice of the reconstruction-blueprint spec: it proves a full manifest can
/// be produced with zero <see cref="MediaBlueprint.Encodes"/> entries.
/// </summary>
public interface IMediaBlueprintBuilder
{
    /// <summary>
    /// Build the identity + source blocks from <paramref name="source"/> with
    /// an empty <see cref="MediaBlueprint.Encodes"/> list. Callers append
    /// encode entries once a preset run completes.
    /// </summary>
    MediaBlueprint BuildFromSource(MediaInfo source, BlueprintIdentity identity);
}
