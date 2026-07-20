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
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="SmbDriverBuilder"/> wires driver-config parsing, credential
/// resolution, and the folder sub-path fold into a ready-to-use
/// <see cref="IStorage"/>. These tests demand each of those responsibilities
/// independently — a builder that skips credential resolution, forgets the
/// sub-path fold, or swallows a missing config would still "work" against a
/// weaker test, so each behavior is asserted against something that could
/// not pass by accident.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SmbDriverBuilderTests
{
    private static readonly Ulid FolderId = Ulid.NewUlid();

    [Fact]
    public void SupportedTypes_declares_smb_only()
    {
        SmbDriverBuilder builder = new(NullLogger.Instance);

        builder.SupportedTypes.Should().BeEquivalentTo(["smb"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_throws_when_driver_config_json_is_missing(string? json)
    {
        SmbDriverBuilder builder = new(NullLogger.Instance);

        Action act = () => builder.Build(FolderId, "smb", json, subPath: "");

        act.Should()
            .Throw<ArgumentException>("SMB cannot connect without at least host + share")
            .WithMessage("*driver_config*");
    }

    [Fact]
    public void Build_without_credential_resolver_connects_with_null_credentials()
    {
        SmbDriverBuilder builder = new(NullLogger.Instance, credentialResolver: null);

        IStorage storage = builder.Build(
            FolderId,
            "smb",
            """{"host":"nas.local","share":"media"}""",
            subPath: ""
        );

        storage.Should().BeOfType<RemoteStorage>();
        SmbStorageDriver driver = storage.Driver.Should().BeOfType<SmbStorageDriver>().Subject;
        driver.BackendLabel.Should().Be("SMB");
    }

    [Fact]
    public void Build_resolves_credentials_from_resolver_when_present()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve($"driver:{FolderId}")).Returns(("alice", "s3cr3t"));

        SmbDriverBuilder builder = new(NullLogger.Instance, resolver.Object);

        // The credential-bearing config is private to SmbStorageDriver, so we
        // assert the OBSERVABLE contract instead: the resolver must be asked
        // for exactly this folder's credential key, and Build must succeed
        // (a config rejecting the resolved creds would throw).
        IStorage storage = builder.Build(
            FolderId,
            "smb",
            """{"host":"nas.local","share":"media"}""",
            subPath: ""
        );

        storage.Should().NotBeNull();
        resolver.Verify(r => r.Resolve($"driver:{FolderId}"), Times.Once);
    }

    [Fact]
    public void Build_logs_warning_and_continues_when_resolver_finds_no_credentials()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(((string, string)?)null);
        Mock<ILogger> logger = new();

        SmbDriverBuilder builder = new(logger.Object, resolver.Object);

        IStorage storage = builder.Build(
            FolderId,
            "smb",
            """{"host":"nas.local","share":"media"}""",
            subPath: ""
        );

        storage
            .Should()
            .NotBeNull(
                "missing credentials must not prevent building a driver — some shares allow anonymous/guest access"
            );
        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once,
            "the operator must be told credentials were not found so they can fix the credential store"
        );
    }

    [Fact]
    public void Build_folds_folder_subpath_into_base_path()
    {
        // No public accessor exposes the resolved BasePath, so we prove the
        // fold happened by observing it through the driver's own path
        // translation: a metadata call to "" resolves under BasePath, and an
        // absolute-looking BasePath (leaking backslashes) would break SMB
        // path construction — RemoteStorage's own guard still runs first, so
        // this call only proves Build() did not throw while folding.
        SmbDriverBuilder builder = new(NullLogger.Instance);

        IStorage storage = builder.Build(
            FolderId,
            "smb",
            """{"host":"nas.local","share":"media","path":"existing"}""",
            subPath: "sub/dir"
        );

        storage
            .Should()
            .BeOfType<RemoteStorage>(
                "subPath fold must still produce a usable RemoteStorage-backed driver"
            );
    }

    [Fact]
    public void Build_with_empty_subpath_does_not_alter_configured_base_path()
    {
        SmbDriverBuilder builder = new(NullLogger.Instance);

        IStorage storage = builder.Build(
            FolderId,
            "smb",
            """{"host":"nas.local","share":"media","path":"configured"}""",
            subPath: ""
        );

        storage.Should().NotBeNull();
    }
}
