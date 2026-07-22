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

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNoMercyStorage_registers_default_local_pipeline()
    {
        ServiceCollection services = new();
        services.AddNoMercyStorage();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStorage>().Should().BeOfType<LocalStorage>();
        provider.GetRequiredService<IStorageDriver>().Should().BeOfType<LocalStorageDriver>();
        provider.GetRequiredService<StorageOptions>().AllowedRoots.Should().BeEmpty();
        provider.GetRequiredService<StoragePathGuard>().Enforced.Should().BeFalse();
    }

    [Fact]
    public void AddNoMercyStorage_applies_options_callback()
    {
        ServiceCollection services = new();
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-svc-test-root"));

        services.AddNoMercyStorage(configure: opts => opts.AllowedRoots.Add(item: root));

        ServiceProvider provider = services.BuildServiceProvider();
        StoragePathGuard guard = provider.GetRequiredService<StoragePathGuard>();

        guard.Enforced.Should().BeTrue();
        guard.AllowedRoots.Should().ContainSingle();
    }

    [Fact]
    public void AddNoMercyStorage_is_idempotent()
    {
        ServiceCollection services = new();
        services.AddNoMercyStorage();
        services.AddNoMercyStorage(configure: opts => opts.AllowedRoots.Add(item: "/should-be-ignored"));

        ServiceProvider provider = services.BuildServiceProvider();

        // First registration wins (TryAdd semantics).
        provider.GetRequiredService<StorageOptions>().AllowedRoots.Should().BeEmpty();
    }
}
