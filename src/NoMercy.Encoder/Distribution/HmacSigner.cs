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
    private readonly byte[] _key = Encoding.UTF8.GetBytes(s: secret);

    /// <summary>
    /// Produces the base64-encoded HMAC-SHA256 signature for the given request primitives.
    /// </summary>
    public string Sign(string method, string path, long timestamp, byte[] body)
    {
        string stringToSign = BuildStringToSign(method: method, path: path, timestamp: timestamp, body: body);
        using HMACSHA256 hmac = new(key: _key);
        byte[] hash = hmac.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: stringToSign));
        return Convert.ToBase64String(inArray: hash);
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

        string expected = Sign(method: method, path: path, timestamp: timestamp, body: body);

        byte[] expectedBytes = Encoding.UTF8.GetBytes(s: expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(s: signature);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(left: expectedBytes, right: actualBytes);
    }

    private static string BuildStringToSign(string method, string path, long timestamp, byte[] body)
    {
        string bodyHash = Convert.ToBase64String(inArray: SHA256.HashData(source: body));
        return $"{method.ToUpperInvariant()}\n{path}\n{timestamp}\n{bodyHash}";
    }
}
