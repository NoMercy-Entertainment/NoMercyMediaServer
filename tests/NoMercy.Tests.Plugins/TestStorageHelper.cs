using NoMercy.Storage;

namespace NoMercy.Tests.Plugins;

internal static class TestStorageHelper
{
    public static IStorageBackend CreateBackend() => new SystemIoStorageBackend();

    public static IStorage CreateStorage(string rootPath)
    {
        IStorageBackend backend = CreateBackend();
        return new LocalStorage(backend, new StoragePathGuard([rootPath], backend));
    }
}
