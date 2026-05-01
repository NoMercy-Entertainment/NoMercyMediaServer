using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Storage;

/// <summary>
/// Builds a permissive <see cref="LocalStorage"/> for tests that need a
/// real filesystem but don't care about the path-allowlist enforcement.
/// </summary>
internal static class TestStorageFactory
{
    public static LocalStorage CreateLocal()
    {
        LocalStorageDriver driver = new();
        return new LocalStorage(driver, new StoragePathGuard([], driver));
    }
}
