using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

/// <summary>
/// Shareable, importable encoding profile shipped separately from the
/// runtime EncoderProfile table. Presets carry richer metadata (description,
/// author, tags, inheritance) so users can browse a library and pick one,
/// or import community presets from a URL.
///
/// When applied, a preset's <see cref="ProfileJson"/> (serialized
/// <c>EncodingProfile</c>) materializes into a runtime EncoderProfile —
/// the two tables are decoupled so deleting a preset doesn't break running
/// encodes that were already dispatched with its materialized profile.
/// </summary>
[PrimaryKey(nameof(Id))]
[Index(nameof(Name))]
[Index(nameof(IsBuiltIn))]
[Index(nameof(Source))]
[Index(nameof(ParentPresetId))]
public class EncodingPreset : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("name")]
    [MaxLength(256)]
    public required string Name { get; set; }

    [JsonProperty("description")]
    [MaxLength(2048)]
    public string? Description { get; set; }

    [JsonProperty("author")]
    [MaxLength(256)]
    public string? Author { get; set; }

    /// <summary>Comma-separated tag list (e.g. "anime,1080p,archival").</summary>
    [JsonProperty("tags")]
    [MaxLength(1024)]
    public string? Tags { get; set; }

    /// <summary>
    /// Serialized <c>EncodingProfile</c> JSON. The preset resolver deserializes
    /// this and walks the parent chain (if any) to produce the effective
    /// profile a caller can hand to the encoder.
    /// </summary>
    [JsonProperty("profile_json")]
    public required string ProfileJson { get; set; }

    /// <summary>
    /// Optional parent preset. When set, resolving this preset starts from
    /// the parent's profile and overlays fields present in <see cref="ProfileJson"/>
    /// that differ from their defaults. Lets users author thin "override"
    /// presets that tweak a base preset's CRF / codec / etc. without
    /// duplicating the full profile.
    /// </summary>
    [JsonProperty("parent_preset_id")]
    public Ulid? ParentPresetId { get; set; }

    [JsonIgnore]
    public EncodingPreset? Parent { get; set; }

    /// <summary>Built-in presets are seeded from JSON shipped with the server
    /// and cannot be deleted from the UI — only disabled.</summary>
    [JsonProperty("is_built_in")]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Origin of the preset: "db" for user-created/imported, "file" for
    /// presets loaded from disk at startup, "community" for remotely fetched.
    /// </summary>
    [JsonProperty("source")]
    [MaxLength(64)]
    public string Source { get; set; } = "db";
}
