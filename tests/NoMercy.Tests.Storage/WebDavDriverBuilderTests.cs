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
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="WebDavDriverBuilder"/>'s credential-resolution fallback: when a
/// resolver is configured but has no stored credentials for this folder, the
/// builder must still produce a usable (anonymous) WebDAV driver instead of
/// throwing — many WebDAV servers (public shares, some Nextcloud setups)
/// allow anonymous access, and refusing to build here would break folders
/// that worked fine before a resolver was even wired up.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class WebDavDriverBuilderTests
{
    private static readonly Ulid FolderId = Ulid.NewUlid();

    [Fact]
    public void Build_falls_back_to_anonymous_and_warns_when_resolver_finds_no_credentials()
    {
        Mock<ICredentialResolver> resolver = new();
        resolver.Setup(expression: r => r.Resolve(It.IsAny<string>())).Returns(value: ((string, string)?)null);
        Mock<ILogger> logger = new();
        WebDavDriverBuilder builder = new(logger: logger.Object, credentialResolver: resolver.Object);

        IStorage storage = builder.Build(
            folderId: FolderId,
            driverType: "webdav",
            driverConfigJson: """{"url":"https://dav.example.com/remote.php/webdav/"}""",
            subPath: ""
        );

        storage
            .Should()
            .BeOfType<RemoteStorage>(
                because: "missing credentials must not prevent building a WebDAV driver"
            );
        logger.Verify(
            expression: l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once,
            failMessage: "the operator must be told credentials were not found so they know the driver is running anonymously"
        );
    }

    [Fact]
    public void Build_without_a_credential_resolver_connects_anonymously()
    {
        WebDavDriverBuilder builder = new(logger: NullLogger.Instance, credentialResolver: null);

        IStorage storage = builder.Build(
            folderId: FolderId,
            driverType: "webdav",
            driverConfigJson: """{"url":"https://dav.example.com/remote.php/webdav/"}""",
            subPath: ""
        );

        storage.Should().BeOfType<RemoteStorage>();
    }
}
