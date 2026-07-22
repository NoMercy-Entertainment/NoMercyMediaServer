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

using FluentAssertions;
using NoMercy.Service.Hosting;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Xunit;

namespace NoMercy.Tests.Service.Hosting;

/// <summary>
/// <see cref="BootstrapStorageFactory"/> builds the pre-DI storage pair used for
/// seed calls that run before the container is available. It must always hand
/// back a usable <see cref="LocalStorageDriver"/>-backed pair — a null or
/// mismatched pair here would NRE the very first seed call on every boot.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class BootstrapStorageFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNullStorageAndDriver()
    {
        (IStorage storage, IStorageDriver driver) = BootstrapStorageFactory.Create();

        storage.Should().NotBeNull();
        driver.Should().NotBeNull();
    }

    [Fact]
    public void Create_ReturnsLocalStorageBackedByLocalStorageDriver()
    {
        (IStorage storage, IStorageDriver driver) = BootstrapStorageFactory.Create();

        driver.Should().BeOfType<LocalStorageDriver>();
        storage.Should().BeOfType<LocalStorage>();
    }

    [Fact]
    public void Create_CalledTwice_ReturnsIndependentInstances()
    {
        (IStorage storage1, IStorageDriver driver1) = BootstrapStorageFactory.Create();
        (IStorage storage2, IStorageDriver driver2) = BootstrapStorageFactory.Create();

        storage1.Should().NotBeSameAs(unexpected: storage2);
        driver1.Should().NotBeSameAs(unexpected: driver2);
    }
}
