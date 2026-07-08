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

namespace NoMercy.Storage.Drivers.S3;

/// <summary>
/// Shared SigV4 signing primitives used by both the upload and read paths.
/// All methods are pure — no I/O, no side effects.
/// </summary>
internal static class S3SigV4
{
    // -----------------------------------------------------------------------
    // Key derivation / HMAC
    // -----------------------------------------------------------------------

    internal static byte[] HmacSha256(byte[] key, string data) =>
        new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));

    internal static byte[] DeriveSigningKey(
        string secret,
        string dateStamp,
        string region,
        string service = "s3"
    )
    {
        byte[] kSecret = Encoding.UTF8.GetBytes("AWS4" + secret);
        byte[] kDate = HmacSha256(kSecret, dateStamp);
        byte[] kRegion = HmacSha256(kDate, region);
        byte[] kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    // -----------------------------------------------------------------------
    // Path encoding
    // -----------------------------------------------------------------------

    /// <summary>
    /// Percent-encode every key segment the same way AWS/MinIO expect —
    /// '/' stays as a path separator, everything else uses uppercase
    /// percent-encoding (RFC 3986). Spaces become %20, not '+'.
    /// </summary>
    internal static string EscapeKey(string key)
    {
        StringBuilder sb = new();
        foreach (string segment in key.Split('/'))
        {
            if (sb.Length > 0)
                sb.Append('/');
            sb.Append(Uri.EscapeDataString(segment));
        }
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Authorization-header signing (GET / HEAD / DELETE)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Build an <c>Authorization</c> header value for a request with an
    /// unsigned / empty payload. Handles GET, HEAD, DELETE.
    /// </summary>
    internal static (string AuthorizationHeader, string AmzDate) SignHeaderRequest(
        string method,
        string endpoint,
        string bucket,
        string key,
        string canonicalQueryString,
        string region,
        string accessKey,
        string secretKey,
        DateTime utcNow
    )
    {
        string host = HostFromEndpoint(endpoint);
        string canonicalUri = "/" + Uri.EscapeDataString(bucket) + "/" + EscapeKey(key);

        string amzDate = utcNow.ToString("yyyyMMddTHHmmssZ");
        string dateStamp = utcNow.ToString("yyyyMMdd");

        const string payloadHash =
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        string canonicalHeaders =
            $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        string canonicalRequest = string.Join(
            "\n",
            method,
            canonicalUri,
            canonicalQueryString,
            canonicalHeaders,
            signedHeaders,
            payloadHash
        );

        string credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        string stringToSign = string.Join(
            "\n",
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))
                .ToLowerInvariant()
        );

        byte[] signingKey = DeriveSigningKey(secretKey, dateStamp, region);
        string signature = Convert
            .ToHexString(HmacSha256(signingKey, stringToSign))
            .ToLowerInvariant();

        string authHeader =
            $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, "
            + $"SignedHeaders={signedHeaders}, Signature={signature}";

        return (authHeader, amzDate);
    }

    // -----------------------------------------------------------------------
    // Query-string presigned URL
    // -----------------------------------------------------------------------

    /// <summary>
    /// Build a SigV4 query-string presigned GET URL.
    /// TTL is clamped to [60s, 86400s].
    /// </summary>
    internal static Uri BuildPresignedGetUrl(
        string endpoint,
        string bucket,
        string key,
        string region,
        string accessKey,
        string secretKey,
        TimeSpan ttl,
        DateTime utcNow
    )
    {
        int expiresSeconds = (int)Math.Clamp(ttl.TotalSeconds, 60, 86400);

        string host = HostFromEndpoint(endpoint);
        string canonicalUri = "/" + Uri.EscapeDataString(bucket) + "/" + EscapeKey(key);

        string amzDate = utcNow.ToString("yyyyMMddTHHmmssZ");
        string dateStamp = utcNow.ToString("yyyyMMdd");
        string credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        string credentialParam = Uri.EscapeDataString($"{accessKey}/{credentialScope}");

        // Query-string params must be sorted alphabetically
        string canonicalQs =
            $"X-Amz-Algorithm=AWS4-HMAC-SHA256"
            + $"&X-Amz-Credential={credentialParam}"
            + $"&X-Amz-Date={amzDate}"
            + $"&X-Amz-Expires={expiresSeconds}"
            + $"&X-Amz-SignedHeaders=host";

        string canonicalHeaders = $"host:{host}\n";
        const string signedHeaders = "host";
        const string payloadHash = "UNSIGNED-PAYLOAD";

        string canonicalRequest = string.Join(
            "\n",
            "GET",
            canonicalUri,
            canonicalQs,
            canonicalHeaders,
            signedHeaders,
            payloadHash
        );

        string stringToSign = string.Join(
            "\n",
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))
                .ToLowerInvariant()
        );

        byte[] signingKey = DeriveSigningKey(secretKey, dateStamp, region);
        string signature = Convert
            .ToHexString(HmacSha256(signingKey, stringToSign))
            .ToLowerInvariant();

        string url =
            endpoint.TrimEnd('/')
            + canonicalUri
            + "?"
            + canonicalQs
            + $"&X-Amz-Signature={signature}";

        return new(url);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    internal static string HostFromEndpoint(string endpoint)
    {
        Uri uri = new(endpoint.TrimEnd('/'));
        return uri.Host + (uri.IsDefaultPort ? string.Empty : $":{uri.Port}");
    }
}
