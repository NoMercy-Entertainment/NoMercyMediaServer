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

using Newtonsoft.Json;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// One JSON file per media item — the functional blueprint for reversing
/// every encode run against it. Replaces the old per-preset
/// <c>manifest.json</c> + <c>reconstruction.json</c> pair (see
/// <c>.claude/specs/reconstruction-blueprint/SPEC.md</c>).
/// <see cref="Identity"/> and <see cref="Source"/> are shared data written
/// once; each preset run appends an entry to <see cref="Encodes"/>.
/// </summary>
public record MediaBlueprint(
    [property: JsonProperty(propertyName: "version")] int Version,
    [property: JsonProperty(propertyName: "identity")] BlueprintIdentity Identity,
    [property: JsonProperty(propertyName: "source")] BlueprintSource Source,
    [property: JsonProperty(propertyName: "encodes")] IReadOnlyList<BlueprintEncode> Encodes
);
