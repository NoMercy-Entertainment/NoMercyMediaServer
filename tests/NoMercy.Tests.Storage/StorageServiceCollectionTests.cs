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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Factory;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Composition-root contract for the storage DI registration. Exercises the
/// real <see cref="ServiceCollectionExtensions.AddNoMercyStorage"/> wiring
/// (Slice 11.2): the factory resolves and every built-in driver builder is
/// registered as a keyed-by-SupportedTypes service, so adding a driver needs
/// only a new registration — never an edit to StorageFactory.
/// </summary>
public class StorageServiceCollectionTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton(serviceType: typeof(ILogger<>), implementationType: typeof(NullLogger<>));
        services.AddNoMercyStorage();
        return services.BuildServiceProvider(options: new ServiceProviderOptions { ValidateOnBuild = true });
    }

    [Fact]
    public void AddNoMercyStorage_resolves_the_storage_factory()
    {
        using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IStorageFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddNoMercyStorage_registers_builders_for_every_built_in_driver_type()
    {
        using ServiceProvider provider = BuildProvider();

        List<string> keys = provider
            .GetServices<IStorageDriverBuilder>()
            .SelectMany(selector: builder => builder.SupportedTypes)
            .ToList();

        keys.Should().BeEquivalentTo(expectation: ["local", "nfs", "s3", "r2", "webdav"]);
        keys.Should().OnlyHaveUniqueItems();
    }
}
