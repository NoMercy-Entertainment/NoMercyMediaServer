using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace NoMercy.Storage;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IStorage"/> backed by
    /// <see cref="LocalStorage"/> over <see cref="System.IO"/>. Safe to
    /// call multiple times — uses <c>TryAdd</c> for every binding so
    /// the first registration wins. Hosts can override
    /// <see cref="IStorageBackend"/> or <see cref="IStorage"/> with
    /// their own implementation by registering it before this call.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">
    /// Optional callback to populate <see cref="StorageOptions"/> (most
    /// importantly <see cref="StorageOptions.AllowedRoots"/>). When
    /// omitted the path guard runs in permissive mode.
    /// </param>
    public static IServiceCollection AddNoMercyStorage(
        this IServiceCollection services,
        Action<StorageOptions>? configure = null
    )
    {
        StorageOptions opts = new();
        configure?.Invoke(opts);
        services.TryAddSingleton(opts);

        services.TryAddSingleton<IStorageBackend, SystemIoStorageBackend>();

        services.TryAddSingleton(sp => new StoragePathGuard(
            sp.GetRequiredService<StorageOptions>().AllowedRoots,
            sp.GetRequiredService<IStorageBackend>()
        ));

        services.TryAddSingleton<IStorage>(sp => new LocalStorage(
            sp.GetRequiredService<IStorageBackend>(),
            sp.GetRequiredService<StoragePathGuard>()
        ));

        services.TryAddSingleton<IStorageFactory>(sp => new StorageFactory(
            sp.GetRequiredService<IStorageBackend>(),
            sp.GetRequiredService<ILogger<StorageFactory>>()
        ));

        return services;
    }
}
