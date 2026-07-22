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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Validation;

namespace NoMercy.Storage;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IStorage"/> backed by
    /// <see cref="LocalStorage"/> over <see cref="System.IO"/>. Safe to
    /// call multiple times — uses <c>TryAdd</c> for every binding so
    /// the first registration wins.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">
    /// Optional callback to populate <see cref="StorageOptions"/>.
    /// </param>
    public static IServiceCollection AddNoMercyStorage(
        this IServiceCollection services,
        Action<StorageOptions>? configure = null
    )
    {
        StorageOptions opts = new();
        configure?.Invoke(obj: opts);
        services.TryAddSingleton(instance: opts);

        services.TryAddSingleton<IStorageDriver, LocalStorageDriver>();

        services.TryAddSingleton(implementationFactory: sp => new StoragePathGuard(
            allowedRoots: sp.GetRequiredService<StorageOptions>().AllowedRoots,
            driver: sp.GetRequiredService<IStorageDriver>()
        ));

        services.TryAddSingleton<IStorage>(implementationFactory: sp => new LocalStorage(
            driver: sp.GetRequiredService<IStorageDriver>(),
            guard: sp.GetRequiredService<StoragePathGuard>()
        ));

        services.AddSingleton<IStorageDriverBuilder>(implementationFactory: sp => new LocalDriverBuilder(
            driver: sp.GetRequiredService<IStorageDriver>()
        ));
        services.AddSingleton<IStorageDriverBuilder>(implementationFactory: sp => new NfsDriverBuilder(
            logger: sp.GetRequiredService<ILogger<NfsDriverBuilder>>()
        ));
        services.AddSingleton<IStorageDriverBuilder>(implementationFactory: sp => new S3DriverBuilder(
            logger: sp.GetRequiredService<ILogger<S3DriverBuilder>>(),
            credentialResolver: sp.GetService<ICredentialResolver>()
        ));
        services.AddSingleton<IStorageDriverBuilder>(implementationFactory: sp => new WebDavDriverBuilder(
            logger: sp.GetRequiredService<ILogger<WebDavDriverBuilder>>(),
            credentialResolver: sp.GetService<ICredentialResolver>()
        ));

        services.TryAddSingleton<IStorageFactory>(implementationFactory: sp => new StorageFactory(
            logger: sp.GetRequiredService<ILogger<StorageFactory>>(),
            builders: sp.GetServices<IStorageDriverBuilder>(),
            driverConfigResolver: sp.GetService<IDriverConfigResolver>()
        ));

        return services;
    }
}
