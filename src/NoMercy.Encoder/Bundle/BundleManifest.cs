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

public record BundleManifest(
    [property: JsonProperty("version")] int Version,
    [property: JsonProperty("encoder_version")] string EncoderVersion,
    [property: JsonProperty("preset_id")] string PresetId,
    [property: JsonProperty("preset_name")] string PresetName,
    [property: JsonProperty("preset_slug")] string PresetSlug,
    [property: JsonProperty("media_type")] string MediaType, // "movie" | "episode" | "track"
    [property: JsonProperty("media_id")] long MediaId,
    [property: JsonProperty("media_external_id")] string? MediaExternalId,
    [property: JsonProperty("media_folder")] string MediaFolder,
    [property: JsonProperty("container")] string Container,
    [property: JsonProperty("created_at")] DateTime CreatedAt,
    [property: JsonProperty("completed_at")] DateTime? CompletedAt,
    [property: JsonProperty("media_key")] string MediaKey,
    [property: JsonProperty("files")] IReadOnlyList<string> Files,
    // Null for every manifest written before fingerprinting shipped. The
    // reconciler treats that as "same profile, unknown version" rather than
    // "profile changed" — see NoMercy.Encoder.Reconciliation.EncodeReconciler.
    [property: JsonProperty("profile_fingerprint")] string? ProfileFingerprint = null
);
