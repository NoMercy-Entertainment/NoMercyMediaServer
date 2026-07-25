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

using System.Security.Cryptography;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Computes the canonical fingerprint for an Ed25519 public key.
/// The fingerprint is the lowercase hex-encoded SHA-256 digest of the raw
/// public key bytes (32 bytes for Ed25519), producing a stable 64-character
/// string used as the primary key in <c>trusted_publisher_keys</c>.
/// </summary>
public static class PublicKeyFingerprint
{
    /// <summary>
    /// Returns the lowercase hex SHA-256 of <paramref name="publicKeyBytes"/>.
    /// </summary>
    /// <param name="publicKeyBytes">Raw Ed25519 public key bytes (must be exactly 32 bytes).</param>
    /// <returns>64-character lowercase hex string.</returns>
    public static string Compute(byte[] publicKeyBytes)
    {
        byte[] hash = SHA256.HashData(publicKeyBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
