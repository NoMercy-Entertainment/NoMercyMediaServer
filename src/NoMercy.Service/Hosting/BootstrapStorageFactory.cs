#region License
// Copyright NoMercy (c) 2026. All rights reserved.
#endregion

using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Service.Hosting;

public static class BootstrapStorageFactory
{
    public static (IStorage storage, IStorageDriver driver) Create()
    {
        IStorageDriver driver = new LocalStorageDriver();
        StoragePathGuard guard = new([], driver);
        IStorage storage = new LocalStorage(driver, guard);
        
        return (storage, driver);
    }
}
