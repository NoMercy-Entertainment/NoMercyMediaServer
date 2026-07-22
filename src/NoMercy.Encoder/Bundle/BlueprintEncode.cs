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
/// One preset's encode record — see spec Part 3. A re-encode with a new
/// preset appends a new entry rather than replacing this one.
/// </summary>
public record BlueprintEncode(
    [property: JsonProperty(propertyName: "preset_slug")] string PresetSlug,
    [property: JsonProperty(propertyName: "preset_id")] string PresetId,
    /// <summary>What the reconciler reads — moved here from the old manifest.json.</summary>
    [property: JsonProperty(propertyName: "profile_fingerprint")] string? ProfileFingerprint,
    [property: JsonProperty(propertyName: "encoder_version")] string EncoderVersion,
    /// <summary>The source container a rebuild produces — never the HLS/fMP4 output.</summary>
    [property: JsonProperty(propertyName: "target_container")] string TargetContainer,
    [property: JsonProperty(propertyName: "output_location")] string OutputLocation,
    [property: JsonProperty(propertyName: "created_at")] DateTime CreatedAt,
    [property: JsonProperty(propertyName: "completed_at")] DateTime CompletedAt,
    [property: JsonProperty(propertyName: "tracks")] IReadOnlyList<BlueprintTrack> Tracks,
    /// <summary>Generated from the structured track mapping, not frozen as a string.</summary>
    [property: JsonProperty(
        propertyName: "reconstruction_command_template"
    )] string? ReconstructionCommandTemplate,
    [property: JsonProperty(propertyName: "lossy_warnings")] IReadOnlyList<string> LossyWarnings
);
