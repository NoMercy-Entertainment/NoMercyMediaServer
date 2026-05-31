namespace NoMercy.Storage;

/// <summary>
/// Resolves a driver instance by its ID to its type and JSON config.
/// Implemented in a higher-level project that has DB access;
/// injected into <see cref="IStorageFactory"/> at DI registration time.
/// </summary>
public interface IDriverConfigResolver
{
    /// <summary>
    /// Returns (type, configJson) for the given <paramref name="driverId"/>,
    /// or <c>null</c> if no Driver row exists with that ID.
    /// </summary>
    (string Type, string? ConfigJson)? Resolve(Ulid driverId);
}
