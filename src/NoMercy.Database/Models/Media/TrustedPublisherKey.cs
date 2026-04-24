using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

/// <summary>
/// A trusted Ed25519 public key used to verify signatures on imported encoding
/// profiles. The fingerprint is the lowercase hex SHA-256 of the raw public key
/// bytes and acts as the primary key so lookups from the profile's
/// <c>PublisherKeyFingerprint</c> field are a direct PK hit.
/// </summary>
[PrimaryKey(nameof(Fingerprint))]
public sealed class TrustedPublisherKey
{
    /// <summary>Lowercase hex SHA-256 of the raw Ed25519 public key bytes (64 chars).</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(64)]
    [JsonProperty("fingerprint")]
    public string Fingerprint { get; init; } = "";

    /// <summary>Human-readable label shown in the admin UI.</summary>
    [MaxLength(256)]
    [JsonProperty("label")]
    public string Label { get; set; } = "";

    /// <summary>Raw Ed25519 public key bytes encoded as base64.</summary>
    [MaxLength(256)]
    [JsonProperty("public_key_base64")]
    public string PublicKeyBase64 { get; set; } = "";

    /// <summary>UTC timestamp when this key was added.</summary>
    [JsonProperty("added_at")]
    public DateTime AddedAt { get; init; } = DateTime.UtcNow;

    /// <summary>User id (Keycloak sub) of the admin who registered the key.</summary>
    [MaxLength(256)]
    [JsonProperty("added_by")]
    public string AddedBy { get; init; } = "";
}
