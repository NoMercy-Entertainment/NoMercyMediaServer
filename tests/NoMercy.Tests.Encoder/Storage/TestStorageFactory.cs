namespace NoMercy.Tests.Encoder.Storage;

using NoMercy.Storage;

/// <summary>
/// Builds a permissive <see cref="LocalStorage"/> for tests that need a
/// real filesystem but don't care about the path-allowlist enforcement.
/// </summary>
internal static class TestStorageFactory
{
    public static LocalStorage CreateLocal()
    {
        SystemIoStorageBackend backend = new();
        return new LocalStorage(backend, new StoragePathGuard([], backend));
    }
}
