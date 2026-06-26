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

using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Verifies the Ed25519 signature on an imported profile JSON payload against
/// the server's <c>trusted_publisher_keys</c> table.
/// </summary>
public interface IProfileSignatureVerifier
{
    /// <summary>
    /// Verify the detached Ed25519 signature on a profile JSON payload.
    /// </summary>
    /// <param name="profileJson">The canonical profile JSON string that was signed.</param>
    /// <param name="fingerprint">Lowercase hex SHA-256 of the signer's public key.</param>
    /// <param name="base64Signature">Base64-encoded Ed25519 signature over SHA-256(profileJson).</param>
    /// <param name="keyLookup">Callback that resolves a fingerprint to a trusted key row; returns null when unknown.</param>
    /// <returns>Null on success; a populated <see cref="EncoderRule"/> describing the rejection on failure.</returns>
    EncoderRule? Verify(
        string profileJson,
        string fingerprint,
        string base64Signature,
        Func<string, TrustedPublisherKey?> keyLookup
    );
}
