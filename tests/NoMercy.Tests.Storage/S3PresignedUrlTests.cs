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

    private static readonly DateTime FixedUtc = new(2013, 5, 24, 0, 0, 0, DateTimeKind.Utc);

    // -----------------------------------------------------------------------
    // Structure tests (no live network needed)
    // -----------------------------------------------------------------------

    [Fact]
    public void PresignedUrl_contains_required_params()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "test/file.mp4",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );

        string qs = url.Query;

        qs.Should().Contain("X-Amz-Algorithm=AWS4-HMAC-SHA256");
        qs.Should().Contain("X-Amz-Credential=");
        qs.Should().Contain("X-Amz-Date=");
        qs.Should().Contain("X-Amz-Expires=3600");
        qs.Should().Contain("X-Amz-SignedHeaders=host");
        qs.Should().Contain("X-Amz-Signature=");
    }

    [Fact]
    public void PresignedUrl_path_contains_bucket_and_key()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "shows/Breaking.Bad/S01E01.mkv",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromMinutes(30),
            FixedUtc
        );

        url.AbsolutePath.Should().Contain(Bucket);
        url.AbsolutePath.Should().Contain("Breaking.Bad");
        url.AbsolutePath.Should().Contain("S01E01.mkv");
    }

    [Fact]
    public void PresignedUrl_ttl_clamped_to_minimum_60s()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromSeconds(10),
            FixedUtc
        );

        url.Query.Should().Contain("X-Amz-Expires=60");
    }

    [Fact]
    public void PresignedUrl_ttl_clamped_to_maximum_86400s()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromDays(7),
            FixedUtc
        );

        url.Query.Should().Contain("X-Amz-Expires=86400");
    }

    [Fact]
    public void PresignedUrl_credential_scope_is_correct()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );

        // The credential param encodes AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request
        string expected = Uri.EscapeDataString($"{AccessKey}/20130524/{Region}/s3/aws4_request");
        url.Query.Should().Contain(expected);
    }

    [Fact]
    public void PresignedUrl_date_matches_fixed_utc()
    {
        Uri url = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );

        url.Query.Should().Contain("X-Amz-Date=20130524T000000Z");
    }

    [Fact]
    public void PresignedUrl_signature_is_deterministic_for_same_inputs()
    {
        Uri url1 = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );
        Uri url2 = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );

        url1.ToString().Should().Be(url2.ToString());
    }

    [Fact]
    public void PresignedUrl_different_keys_produce_different_signatures()
    {
        Uri url1 = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file-a.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );
        Uri url2 = S3SigV4.BuildPresignedGetUrl(
            Endpoint,
            Bucket,
            "file-b.bin",
            Region,
            AccessKey,
            SecretKey,
            TimeSpan.FromHours(1),
            FixedUtc
        );

        url1.ToString().Should().NotBe(url2.ToString());
    }

    // -----------------------------------------------------------------------
    // S3StorageDriver.TryGetPresignedUrlAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryGetPresignedUrlAsync_returns_url_when_credentials_present()
    {
        using S3StorageDriver driver = new(
            Bucket,
            Region,
            prefix: null,
            endpoint: Endpoint,
            accessKey: AccessKey,
            secretKey: SecretKey
        );

        Uri? url = await driver.TryGetPresignedUrlAsync(
            "media/file.mp4",
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        url.Should().NotBeNull();
        url!.ToString().Should().StartWith(Endpoint);
        url.Query.Should().Contain("X-Amz-Signature=");
    }

    [Fact]
    public async Task TryGetPresignedUrlAsync_returns_null_without_credentials()
    {
        // SDK-injection constructor (test path) — no raw credentials
        using S3StorageDriver driver = new(
            Bucket,
            "us-east-1",
            prefix: null,
            endpoint: null,
            accessKey: null,
            secretKey: null
        );

        Uri? url = await driver.TryGetPresignedUrlAsync(
            "media/file.mp4",
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        url.Should().BeNull();
    }
}
