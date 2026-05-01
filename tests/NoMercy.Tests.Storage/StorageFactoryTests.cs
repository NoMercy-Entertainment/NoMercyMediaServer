using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Storage;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="StorageFactory"/> tests. The factory's public API now takes a
/// <c>(folderId, driverId?, folderPath)</c> triple. Driver type + config are
/// resolved via <see cref="IDriverConfigResolver"/>; passing <c>null</c> as
/// <c>driverId</c> selects the built-in local driver.
/// </summary>
public class StorageFactoryTests
{
    private static Mock<IStorageDriver> BackendMock()
    {
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        driver
            .Setup(b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
        return driver;
    }

    private static StorageFactory Factory(
        Mock<IStorageDriver>? driver = null,
        IDriverConfigResolver? resolver = null
    )
    {
        Mock<IStorageDriver> b = driver ?? BackendMock();
        return new StorageFactory(b.Object, NullLogger<StorageFactory>.Instance, resolver);
    }

    // Helper: build a resolver stub that maps a Ulid to a (type, configJson) pair.
    private static IDriverConfigResolver StubResolver(string type, string? config = null)
    {
        Mock<IDriverConfigResolver> mock = new();
        mock.Setup(r => r.Resolve(It.IsAny<Ulid>()))
            .Returns((type, config));
        return mock.Object;
    }

    // -----------------------------------------------------------------------
    // Local driver (driverId == null)
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_null_driverId_returns_IStorage()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();

        IStorage storage = factory.For(Ulid.NewUlid(), null, root);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    [Fact]
    public void For_local_null_driverId_allows_paths_under_folder_root()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(id, null, root);

        string inside = Path.Combine(root, "subdir", "file.bin");
        bool exists = storage.Exists(inside);
        exists.Should().BeFalse();
    }

    [Fact]
    public void For_local_null_driverId_rejects_paths_outside_folder_root()
    {
        Mock<IStorageDriver> driver = BackendMock();
        StorageFactory factory = Factory(driver);

        string root = Path.Combine(Path.GetTempPath(), "nm-factory-test-" + Ulid.NewUlid());
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(id, null, root);

        string outside = Path.GetTempPath();
        Action act = () => storage.Exists(outside);

        act.Should().Throw<StoragePathNotAllowedException>();
    }

    // -----------------------------------------------------------------------
    // Local driver via resolver
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_via_resolver_returns_IStorage()
    {
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(resolver: StubResolver("local"));
        string root = Path.GetTempPath();

        IStorage storage = factory.For(Ulid.NewUlid(), driverId, root);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    // -----------------------------------------------------------------------
    // SMB — OS-mount driver, still treated as local
    // -----------------------------------------------------------------------

    [Fact]
    public void For_smb_returns_IStorage()
    {
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(resolver: StubResolver("smb"));
        string root = Path.GetTempPath();

        IStorage storage = factory.For(Ulid.NewUlid(), driverId, root);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    // -----------------------------------------------------------------------
    // NFS — in-process driver; requires valid server + export in config
    // -----------------------------------------------------------------------

    [Fact]
    public void For_nfs_without_config_throws_ArgumentException()
    {
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(resolver: StubResolver("nfs", null));

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_nfs_with_valid_config_parses_without_throwing_at_construction()
    {
        Ulid driverId = Ulid.NewUlid();
        string json = """{"server":"nas.local","export":"/media"}""";
        StorageFactory factory = Factory(resolver: StubResolver("nfs", json));

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        // Should throw DllNotFoundException (no libnfs) but NOT ArgumentException.
        act.Should().NotThrow<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // S3 / R2 — null config throws ArgumentException
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s3")]
    [InlineData("r2")]
    public void For_s3_r2_null_config_throws_ArgumentException(string driverType)
    {
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(resolver: StubResolver(driverType, null));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(id, driverId, Path.GetTempPath());

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("s3")]
    public void For_s3_valid_config_returns_RemoteStorage(string driverType)
    {
        Ulid driverId = Ulid.NewUlid();
        string json =
            $"{{\"bucket\":\"test\",\"region\":\"us-east-1\",\"endpoint\":\"http://localhost:9000\"}}";
        StorageFactory factory = Factory(resolver: StubResolver(driverType, json));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(id, driverId, Path.GetTempPath());

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_r2_without_endpoint_throws_ArgumentException()
    {
        Ulid driverId = Ulid.NewUlid();
        string json = "{\"bucket\":\"test\",\"region\":\"auto\"}";
        StorageFactory factory = Factory(resolver: StubResolver("r2", json));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(id, driverId, Path.GetTempPath());

        act.Should().Throw<ArgumentException>().WithMessage("*endpoint*");
    }

    // -----------------------------------------------------------------------
    // WebDAV
    // -----------------------------------------------------------------------

    [Fact]
    public void For_webdav_without_config_throws_ArgumentException()
    {
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(resolver: StubResolver("webdav", null));

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_webdav_missing_url_throws_ArgumentException()
    {
        Ulid driverId = Ulid.NewUlid();
        string json = """{"username":"user"}""";
        StorageFactory factory = Factory(resolver: StubResolver("webdav", json));

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*url*");
    }

    [Fact]
    public void For_webdav_valid_config_returns_RemoteStorage()
    {
        Ulid driverId = Ulid.NewUlid();
        string json = """{"url":"http://dav.example.com/files/"}""";
        StorageFactory factory = Factory(resolver: StubResolver("webdav", json));

        IStorage storage = factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    // -----------------------------------------------------------------------
    // Unknown driver type
    // -----------------------------------------------------------------------

    [Fact]
    public void For_unknown_type_throws_ArgumentException()
    {
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(resolver: StubResolver("ftp"));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(id, driverId, Path.GetTempPath());

        act.Should().Throw<ArgumentException>().WithMessage("*'ftp'*");
    }

    // -----------------------------------------------------------------------
    // Cache semantics
    // -----------------------------------------------------------------------

    [Fact]
    public void For_repeated_call_returns_same_instance()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid id = Ulid.NewUlid();

        IStorage first = factory.For(id, null, root);
        IStorage second = factory.For(id, null, root);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Invalidate_causes_next_call_to_rebuild()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid id = Ulid.NewUlid();

        IStorage first = factory.For(id, null, root);
        factory.Invalidate(id);
        IStorage second = factory.For(id, null, root);

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Invalidate_only_removes_matching_folder()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid idA = Ulid.NewUlid();
        Ulid idB = Ulid.NewUlid();

        IStorage storageA = factory.For(idA, null, root);
        IStorage storageB = factory.For(idB, null, root);

        factory.Invalidate(idA);

        IStorage storageA2 = factory.For(idA, null, root);
        IStorage storageB2 = factory.For(idB, null, root);

        storageA2.Should().NotBeSameAs(storageA);
        storageB2.Should().BeSameAs(storageB);
    }

    [Fact]
    public void InvalidateAll_clears_entire_cache()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid idA = Ulid.NewUlid();
        Ulid idB = Ulid.NewUlid();

        IStorage a1 = factory.For(idA, null, root);
        IStorage b1 = factory.For(idB, null, root);

        factory.InvalidateAll();

        IStorage a2 = factory.For(idA, null, root);
        IStorage b2 = factory.For(idB, null, root);

        a2.Should().NotBeSameAs(a1);
        b2.Should().NotBeSameAs(b1);
    }

    // -----------------------------------------------------------------------
    // LocalDriverConfig RootPath override
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_with_RootPath_in_config_uses_config_root()
    {
        Mock<IStorageDriver> driver = BackendMock();
        string configRoot = Path.GetTempPath();
        string json = $"{{\"rootPath\": \"{configRoot.Replace("\\", "\\\\")}\"}}";
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(driver, StubResolver("local", json));
        Ulid id = Ulid.NewUlid();

        string folderPath = Path.Combine(configRoot, "sub");
        IStorage storage = factory.For(id, driverId, folderPath);

        string allowed = Path.Combine(configRoot, "some", "file.bin");
        Action act = () => storage.Exists(allowed);
        act.Should().NotThrow<StoragePathNotAllowedException>();
    }

    [Fact]
    public void For_local_with_malformed_configJson_falls_back_to_folderPath()
    {
        Mock<IStorageDriver> driver = BackendMock();
        string root = Path.Combine(Path.GetTempPath(), "nm-factory-fallback-" + Ulid.NewUlid());
        Ulid driverId = Ulid.NewUlid();
        StorageFactory factory = Factory(driver, StubResolver("local", "not-valid-json{{{"));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(id, driverId, root);

        string outside = Path.GetTempPath();
        Action act = () => storage.Exists(outside);
        act.Should().Throw<StoragePathNotAllowedException>();
    }
}
