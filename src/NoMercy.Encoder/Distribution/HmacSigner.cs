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
using System.Text;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Signs and verifies HTTP requests for distributed encoder communication.
///
/// String to sign: "{METHOD}\n{PATH}\n{TIMESTAMP}\n{base64(sha256(body))}"
/// Output signature: base64(hmac_sha256(secret, stringToSign))
///
/// Constant-time comparison via <see cref="CryptographicOperations.FixedTimeEquals"/>
/// prevents timing-oracle attacks.
/// </summary>
public sealed class HmacSigner(string secret)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    /// <summary>
    /// Produces the base64-encoded HMAC-SHA256 signature for the given request primitives.
    /// </summary>
    public string Sign(string method, string path, long timestamp, byte[] body)
    {
        string stringToSign = BuildStringToSign(method, path, timestamp, body);
        using HMACSHA256 hmac = new(_key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a signature against the expected one derived from the request primitives.
    /// Returns false when:
    /// - the signature does not match (wrong key, tampered body, wrong method/path),
    /// - or the timestamp is older than <paramref name="replayWindow"/> (replay attack protection).
    /// </summary>
    public bool Verify(
        string method,
        string path,
        long timestamp,
        byte[] body,
        string signature,
        TimeSpan replayWindow
    )
    {
        long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long ageSeconds = nowSeconds - timestamp;

        if (ageSeconds < 0 || ageSeconds > (long)replayWindow.TotalSeconds)
            return false;

        string expected = Sign(method, path, timestamp, body);

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(signature);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string BuildStringToSign(string method, string path, long timestamp, byte[] body)
    {
        string bodyHash = Convert.ToBase64String(SHA256.HashData(body));
        return $"{method.ToUpperInvariant()}\n{path}\n{timestamp}\n{bodyHash}";
    }
}
