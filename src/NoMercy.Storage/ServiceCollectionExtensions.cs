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
        configure?.Invoke(opts);
        services.TryAddSingleton(opts);

        services.TryAddSingleton<IStorageDriver, LocalStorageDriver>();

        services.TryAddSingleton(sp => new StoragePathGuard(
            sp.GetRequiredService<StorageOptions>().AllowedRoots,
            sp.GetRequiredService<IStorageDriver>()
        ));

        services.TryAddSingleton<IStorage>(sp => new LocalStorage(
            sp.GetRequiredService<IStorageDriver>(),
            sp.GetRequiredService<StoragePathGuard>()
        ));

        services.TryAddSingleton<IStorageFactory>(sp => new StorageFactory(
            sp.GetRequiredService<IStorageDriver>(),
            sp.GetRequiredService<ILogger<StorageFactory>>(),
            sp.GetService<IDriverConfigResolver>(),
            sp.GetService<ICredentialResolver>()
        ));

        return services;
    }
}
