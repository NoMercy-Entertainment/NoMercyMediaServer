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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="S3DriverBuilder"/> is the last line of defense before an S3/R2
/// driver connects with the wrong bucket, region, endpoint, or — worst of
/// all — silently falls through to the AWS SDK's default credential chain
/// (env vars / EC2 IMDS), which is never correct for a self-hosted media
/// server and surfaces as an opaque SDK error deep in the signing path
/// instead of a clear message. Every validation and credential-resolution
/// branch is demanded here.
/// </summary>
[Trait("Category", "Unit")]
public sealed class S3DriverBuilderTests
{
    private static readonly Ulid FolderId = Ulid.NewUlid();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_throws_when_driver_config_json_is_missing(string? json)
    {
        S3DriverBuilder builder = new(NullLogger.Instance);

        Action act = () => builder.Build(FolderId, "s3", json, "");

        act.Should().Throw<ArgumentException>().WithMessage("*driver_config*");
    }

    [Fact]
    public void Build_throws_on_malformed_json()
    {
        S3DriverBuilder builder = new(NullLogger.Instance);

        Action act = () => builder.Build(FolderId, "s3", "{not json", "");

        act.Should()
            .Throw<ArgumentException>()
            .WithInnerException<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Build_throws_on_json_null_literal()
    {
        S3DriverBuilder builder = new(NullLogger.Instance);

        Action act = () => builder.Build(FolderId, "s3", "null", "");

        act.Should().Throw<ArgumentException>().WithMessage("*null*");
    }

    [Fact]
    public void Build_throws_when_bucket_is_missing()
    {
        S3DriverBuilder builder = new(NullLogger.Instance);

        Action act = () => builder.Build(FolderId, "s3", """{"region":"us-east-1"}""", "");

        act.Should().Throw<ArgumentException>().WithMessage("*bucket*");
    }

    [Fact]
    public void Build_throws_when_region_is_missing()
    {
        S3DriverBuilder builder = new(NullLogger.Instance);

        Action act = () => builder.Build(FolderId, "s3", """{"bucket":"media"}""", "");

        act.Should().Throw<ArgumentException>().WithMessage("*region*");
    }

    [Fact]
    public void Build_throws_for_r2_without_endpoint()
    {
        S3DriverBuilder builder = new(NullLogger.Instance);
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(("ak", "sk"));
        S3DriverBuilder builderWithCreds = new(NullLogger.Instance, resolver.Object);

        Action act = () =>
            builderWithCreds.Build(
                FolderId,
                "r2",
                """{"bucket":"media","region":"auto"}""",
                ""
            );

        act.Should()
            .Throw<ArgumentException>("R2 has no meaningful default endpoint the way AWS S3 does")
            .WithMessage("*endpoint*");
    }

    [Fact]
    public void Build_throws_when_no_credential_resolver_is_configured()
    {
        // With no resolver at all, accessKey/secretKey stay null and the
        // "no credentials configured" guard must fire — the driver must
        // NEVER silently fall through to the AWS default credential chain.
        S3DriverBuilder builder = new(NullLogger.Instance, null);

        Action act = () =>
            builder.Build(
                FolderId,
                "s3",
                """{"bucket":"media","region":"us-east-1"}""",
                ""
            );

        act.Should().Throw<ArgumentException>().WithMessage("*no credentials configured*");
    }

    [Fact]
    public void Build_throws_when_resolver_present_but_finds_no_credentials()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(((string, string)?)null);
        S3DriverBuilder builder = new(NullLogger.Instance, resolver.Object);

        Action act = () =>
            builder.Build(
                FolderId,
                "s3",
                """{"bucket":"media","region":"us-east-1"}""",
                ""
            );

        act.Should().Throw<ArgumentException>().WithMessage("*no credentials configured*");
    }

    [Fact]
    public void Build_resolves_credentials_via_explicit_credentials_ref_first()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve("my-custom-ref")).Returns(("ak", "sk"));
        S3DriverBuilder builder = new(NullLogger.Instance, resolver.Object);

        IStorage storage = builder.Build(
            FolderId,
            "s3",
            """{"bucket":"media","region":"us-east-1","credentialsRef":"my-custom-ref"}""",
            ""
        );

        storage.Should().BeOfType<RemoteStorage>();
        resolver.Verify(r => r.Resolve("my-custom-ref"), Times.Once);
        resolver.Verify(
            r => r.Resolve($"driver:{FolderId}"),
            Times.Never,
            "the explicit ref must win — no fallback lookup needed when it resolves"
        );
    }

    [Fact]
    public void Build_falls_back_to_driver_key_when_credentials_ref_does_not_resolve()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve("missing-ref")).Returns(((string, string)?)null);
        resolver.Setup(r => r.Resolve($"driver:{FolderId}")).Returns(("ak", "sk"));
        Mock<ILogger> logger = new();
        S3DriverBuilder builder = new(logger.Object, resolver.Object);

        IStorage storage = builder.Build(
            FolderId,
            "s3",
            """{"bucket":"media","region":"us-east-1","credentialsRef":"missing-ref"}""",
            ""
        );

        storage.Should().NotBeNull();
        resolver.Verify(
            r => r.Resolve($"driver:{FolderId}"),
            Times.Once,
            "must fall back to the per-folder key when the explicit ref is not found"
        );
    }

    [Fact]
    public void Build_resolves_credentials_via_driver_key_when_no_credentials_ref_given()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve($"driver:{FolderId}")).Returns(("ak", "sk"));
        S3DriverBuilder builder = new(NullLogger.Instance, resolver.Object);

        IStorage storage = builder.Build(
            FolderId,
            "s3",
            """{"bucket":"media","region":"us-east-1"}""",
            ""
        );

        storage.Should().NotBeNull();
        resolver.Verify(r => r.Resolve($"driver:{FolderId}"), Times.Once);
    }

    [Fact]
    public void Build_succeeds_for_r2_when_endpoint_and_credentials_are_present()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(("ak", "sk"));
        S3DriverBuilder builder = new(NullLogger.Instance, resolver.Object);

        IStorage storage = builder.Build(
            FolderId,
            "r2",
            """{"bucket":"media","region":"auto","endpoint":"https://accountid.r2.cloudflarestorage.com"}""",
            ""
        );

        storage.Should().BeOfType<RemoteStorage>();
        storage.Driver.Should().BeOfType<S3StorageDriver>();
    }

    [Fact]
    public void SupportedTypes_declares_s3_and_r2()
    {
        S3DriverBuilder builder = new(NullLogger.Instance);

        builder.SupportedTypes.Should().BeEquivalentTo(["s3", "r2"]);
    }
}
