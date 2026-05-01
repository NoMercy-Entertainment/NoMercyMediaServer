using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Plugins;

internal static class TestStorageHelper
{
    public static IStorageDriver CreateBackend() => new LocalStorageDriver();

    public static IStorage CreateStorage(string rootPath)
    {
        IStorageDriver driver = CreateBackend();
        return new LocalStorage(driver, new StoragePathGuard([rootPath], driver));
    }
}
