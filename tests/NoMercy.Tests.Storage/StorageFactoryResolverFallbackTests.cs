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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Factory;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="StorageFactory.For"/> must degrade to the built-in local driver
/// — not throw, not return null — when either no
/// <see cref="IDriverConfigResolver"/> is registered at all, or one is
/// registered but has no row for the requested driver. A self-hosted
/// deployment mid-migration (DB not wired yet, or a dangling DriverId after a
/// deletion race) must still serve local files instead of taking the whole
/// request down.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StorageFactoryResolverFallbackTests
{
    private static Mock<IStorageDriver> BackendMock()
    {
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        driver.Setup(b => b.GetFullPath(It.IsAny<string>())).Returns<string>(Path.GetFullPath);
        driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
        return driver;
    }

    [Fact]
    public void For_with_no_driver_config_resolver_registered_falls_back_to_local()
    {
        Mock<IStorageDriver> driver = BackendMock();
        StorageFactory factory = new(
            driver.Object,
            NullLogger<StorageFactory>.Instance,
            null
        );

        IStorage storage = factory.For(Ulid.NewUlid(), Ulid.NewUlid(), string.Empty);

        storage
            .Should()
            .NotBeNull(
                "with no resolver wired up, the factory must still produce a usable local-backed storage"
            );
    }

    [Fact]
    public void For_with_resolver_returning_null_for_the_driverId_falls_back_to_local()
    {
        Mock<IStorageDriver> driver = BackendMock();
        Mock<IDriverConfigResolver> resolver = new();
        resolver.Setup(r => r.Resolve(It.IsAny<Ulid>())).Returns(((string, string?)?)null);
        StorageFactory factory = new(
            driver.Object,
            NullLogger<StorageFactory>.Instance,
            resolver.Object
        );

        IStorage storage = factory.For(Ulid.NewUlid(), Ulid.NewUlid(), string.Empty);

        storage
            .Should()
            .NotBeNull("a dangling/unknown DriverId must fall back to local, not throw");
    }

    [Fact]
    public void JoinRoot_unrecognized_driver_type_falls_back_to_OS_path_combine()
    {
        string root = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        string result = StorageFactory.JoinRoot(root, "sub", "some-future-driver-type");

        result
            .Should()
            .Be(
                Path.Combine(root, "sub"),
                "an unrecognized driver type must not crash JoinRoot — it degrades to the OS join"
            );
    }
}
