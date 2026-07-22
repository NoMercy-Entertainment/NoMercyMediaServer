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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

/// <summary>
/// Shareable, importable encoding profile — the sole source of truth for
/// encoding configuration. Presets carry richer metadata (description,
/// author, tags, inheritance) so users can browse a library and pick one,
/// or import community presets from a URL.
///
/// A preset's <see cref="ProfileJson"/> (serialized <c>EncodingProfile</c>)
/// is resolved on demand — by <see cref="NoMercy.Encoder.Profiles.PresetResolver"/>,
/// walking <see cref="ParentPresetId"/> chains — wherever a caller only
/// holds the preset's <see cref="Id"/>.
/// </summary>
[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Name))]
[Index(propertyName: nameof(IsBuiltIn))]
[Index(propertyName: nameof(Source))]
[Index(propertyName: nameof(ParentPresetId))]
public class EncodingPreset : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "name")]
    [MaxLength(length: 256)]
    public required string Name { get; set; }

    [JsonProperty(propertyName: "description")]
    [MaxLength(length: 2048)]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "author")]
    [MaxLength(length: 256)]
    public string? Author { get; set; }

    /// <summary>Comma-separated tag list (e.g. "anime,1080p,archival").</summary>
    [JsonProperty(propertyName: "tags")]
    [MaxLength(length: 1024)]
    public string? Tags { get; set; }

    /// <summary>
    /// Serialized <c>EncodingProfile</c> JSON. The preset resolver deserializes
    /// this and walks the parent chain (if any) to produce the effective
    /// profile a caller can hand to the encoder.
    /// </summary>
    [JsonProperty(propertyName: "profile_json")]
    public required string ProfileJson { get; set; }

    /// <summary>
    /// Optional parent preset. When set, resolving this preset starts from
    /// the parent's profile and overlays fields present in <see cref="ProfileJson"/>
    /// that differ from their defaults. Lets users author thin "override"
    /// presets that tweak a base preset's CRF / codec / etc. without
    /// duplicating the full profile.
    /// </summary>
    [JsonProperty(propertyName: "parent_preset_id")]
    public Ulid? ParentPresetId { get; set; }

    [JsonIgnore]
    public EncodingPreset? Parent { get; set; }

    /// <summary>Built-in presets are seeded from JSON shipped with the server
    /// and cannot be deleted from the UI — only disabled.</summary>
    [JsonProperty(propertyName: "is_built_in")]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Origin of the preset: "db" for user-created/imported, "file" for
    /// presets loaded from disk at startup, "community" for remotely fetched.
    /// </summary>
    [JsonProperty(propertyName: "source")]
    [MaxLength(length: 64)]
    public string Source { get; set; } = "db";
}
