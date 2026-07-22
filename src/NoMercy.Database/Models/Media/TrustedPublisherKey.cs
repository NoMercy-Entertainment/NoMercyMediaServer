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
/// A trusted Ed25519 public key used to verify signatures on imported encoding
/// profiles. The fingerprint is the lowercase hex SHA-256 of the raw public key
/// bytes and acts as the primary key so lookups from the profile's
/// <c>PublisherKeyFingerprint</c> field are a direct PK hit.
/// </summary>
[PrimaryKey(propertyName: nameof(Fingerprint))]
public sealed class TrustedPublisherKey
{
    /// <summary>Lowercase hex SHA-256 of the raw Ed25519 public key bytes (64 chars).</summary>
    [Key]
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [MaxLength(length: 64)]
    [JsonProperty(propertyName: "fingerprint")]
    public string Fingerprint { get; init; } = "";

    /// <summary>Human-readable label shown in the admin UI.</summary>
    [MaxLength(length: 256)]
    [JsonProperty(propertyName: "label")]
    public string Label { get; set; } = "";

    /// <summary>Raw Ed25519 public key bytes encoded as base64.</summary>
    [MaxLength(length: 256)]
    [JsonProperty(propertyName: "public_key_base64")]
    public string PublicKeyBase64 { get; set; } = "";

    /// <summary>UTC timestamp when this key was added.</summary>
    [JsonProperty(propertyName: "added_at")]
    public DateTime AddedAt { get; init; } = DateTime.UtcNow;

    /// <summary>User id (Keycloak sub) of the admin who registered the key.</summary>
    [MaxLength(length: 256)]
    [JsonProperty(propertyName: "added_by")]
    public string AddedBy { get; init; } = "";
}
