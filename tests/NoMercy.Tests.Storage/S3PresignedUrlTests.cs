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

using NoMercy.Storage.Drivers.S3;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests for SigV4 query-string presigned URL generation.
//
// Uses known inputs and validates structural correctness of the resulting URL.
// AWS canonical-form rules: https://docs.aws.amazon.com/AmazonS3/latest/API/sigv4-query-string-auth.html
// ============================================================================

public class S3PresignedUrlTests
{
    private const string Endpoint = "http://s3.example.com";
    private const string Bucket = "my-bucket";
    private const string Region = "us-east-1";
    private const string AccessKey = "AKIAIOSFODNN7EXAMPLE";
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private static readonly DateTime FixedUtc = new(year: 2013, month: 5, day: 24, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

    // -----------------------------------------------------------------------
    // Structure tests (no live network needed)
    // -----------------------------------------------------------------------

    [Fact]
    public void PresignedUrl_contains_required_params()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "test/file.mp4",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );

        string qs = url.Query;

        qs.Should().Contain(expected: "X-Amz-Algorithm=AWS4-HMAC-SHA256");
        qs.Should().Contain(expected: "X-Amz-Credential=");
        qs.Should().Contain(expected: "X-Amz-Date=");
        qs.Should().Contain(expected: "X-Amz-Expires=3600");
        qs.Should().Contain(expected: "X-Amz-SignedHeaders=host");
        qs.Should().Contain(expected: "X-Amz-Signature=");
    }

    [Fact]
    public void PresignedUrl_path_contains_bucket_and_key()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "shows/Breaking.Bad/S01E01.mkv",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromMinutes(minutes: 30),
            utcNow: FixedUtc
        );

        url.AbsolutePath.Should().Contain(expected: Bucket);
        url.AbsolutePath.Should().Contain(expected: "Breaking.Bad");
        url.AbsolutePath.Should().Contain(expected: "S01E01.mkv");
    }

    [Fact]
    public void PresignedUrl_ttl_clamped_to_minimum_60s()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromSeconds(seconds: 10),
            utcNow: FixedUtc
        );

        url.Query.Should().Contain(expected: "X-Amz-Expires=60");
    }

    [Fact]
    public void PresignedUrl_ttl_clamped_to_maximum_86400s()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromDays(days: 7),
            utcNow: FixedUtc
        );

        url.Query.Should().Contain(expected: "X-Amz-Expires=86400");
    }

    [Fact]
    public void PresignedUrl_credential_scope_is_correct()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );

        // The credential param encodes AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request
        string expected = Uri.EscapeDataString(stringToEscape: $"{AccessKey}/20130524/{Region}/s3/aws4_request");
        url.Query.Should().Contain(expected: expected);
    }

    [Fact]
    public void PresignedUrl_date_matches_fixed_utc()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );

        url.Query.Should().Contain(expected: "X-Amz-Date=20130524T000000Z");
    }

    [Fact]
    public void PresignedUrl_signature_is_deterministic_for_same_inputs()
    {
        Uri url1 = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );
        Uri url2 = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );

        url1.ToString().Should().Be(expected: url2.ToString());
    }

    [Fact]
    public void PresignedUrl_different_keys_produce_different_signatures()
    {
        Uri url1 = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file-a.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );
        Uri url2 = S3SigV4.BuildPresignedGetUrl(
            endpoint: Endpoint,
            bucket: Bucket,
            key: "file-b.bin",
            region: Region,
            accessKey: AccessKey,
            secretKey: SecretKey,
            ttl: TimeSpan.FromHours(hours: 1),
            utcNow: FixedUtc
        );

        url1.ToString().Should().NotBe(unexpected: url2.ToString());
    }

    // -----------------------------------------------------------------------
    // S3StorageDriver.TryGetPresignedUrlAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryGetPresignedUrlAsync_returns_url_when_credentials_present()
    {
        using S3StorageDriver driver = new(
            bucket: Bucket,
            region: Region,
            prefix: null,
            endpoint: Endpoint,
            accessKey: AccessKey,
            secretKey: SecretKey
        );

        Uri? url = await driver.TryGetPresignedUrlAsync(
            path: "media/file.mp4",
            ttl: TimeSpan.FromHours(hours: 1),
            ct: CancellationToken.None
        );

        url.Should().NotBeNull();
        url!.ToString().Should().StartWith(expected: Endpoint);
        url.Query.Should().Contain(expected: "X-Amz-Signature=");
    }

    [Fact]
    public async Task TryGetPresignedUrlAsync_returns_null_without_credentials()
    {
        // SDK-injection constructor (test path) — no raw credentials
        using S3StorageDriver driver = new(
            bucket: Bucket,
            region: "us-east-1",
            prefix: null,
            endpoint: null,
            accessKey: null,
            secretKey: null
        );

        Uri? url = await driver.TryGetPresignedUrlAsync(
            path: "media/file.mp4",
            ttl: TimeSpan.FromHours(hours: 1),
            ct: CancellationToken.None
        );

        url.Should().BeNull();
    }
}
