using NoMercy.Storage;

namespace NoMercy.Providers.Helpers;

/// <summary>
/// Ambient accessor for <see cref="IStorage"/> and
/// <see cref="IStorageBackend"/> in static provider contexts where
/// constructor injection is not possible. Initialized once at startup
/// via <see cref="Initialize"/> — same pattern as
/// <see cref="HttpClientProvider"/>.
/// </summary>
public static class StorageProvider
{
    private static IStorage? _storage;
    private static IStorageBackend? _backend;

    public static void Initialize(IStorage storage, IStorageBackend backend)
    {
        _storage = storage;
        _backend = backend;
    }

    internal static void Reset()
    {
        _storage = null;
        _backend = null;
    }

    public static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "StorageProvider has not been initialized. Call StorageProvider.Initialize() at startup."
        );

    public static IStorageBackend Backend =>
        _backend
        ?? throw new InvalidOperationException(
            "StorageProvider has not been initialized. Call StorageProvider.Initialize() at startup."
        );
}
