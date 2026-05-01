using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Storage;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

public class StorageFactoryTests
{
    private static Mock<IStorageBackend> BackendMock()
    {
        Mock<IStorageBackend> backend = new(MockBehavior.Loose);
        backend
            .Setup(b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        backend.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
        return backend;
    }

    private static StorageFactory Factory(Mock<IStorageBackend>? backend = null)
    {
        Mock<IStorageBackend> b = backend ?? BackendMock();
        return new StorageFactory(b.Object, NullLogger<StorageFactory>.Instance);
    }

    // -----------------------------------------------------------------------
    // Local backend
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_returns_IStorage()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();

        IStorage storage = factory.For(Ulid.NewUlid(), "local", null, root);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    [Fact]
    public void For_local_allows_paths_under_folder_root()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(id, "local", null, root);

        string inside = Path.Combine(root, "subdir", "file.bin");
        // Validate succeeds — no exception thrown
        bool exists = storage.Exists(inside);
        exists.Should().BeFalse(); // file doesn't actually exist, but path is allowed
    }

    [Fact]
    public void For_local_rejects_paths_outside_folder_root()
    {
        Mock<IStorageBackend> backend = BackendMock();
        StorageFactory factory = Factory(backend);

        // Use a temp subdir so the parent is outside the allowed root
        string root = Path.Combine(Path.GetTempPath(), "nm-factory-test-" + Ulid.NewUlid());
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(id, "local", null, root);

        string outside = Path.GetTempPath(); // parent of root — not under root
        Action act = () => storage.Exists(outside);

        act.Should().Throw<StoragePathNotAllowedException>();
    }

    // -----------------------------------------------------------------------
    // SMB — OS-mount backend, still treated as local
    // -----------------------------------------------------------------------

    [Fact]
    public void For_smb_returns_IStorage()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();

        IStorage storage = factory.For(Ulid.NewUlid(), "smb", null, root);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    // -----------------------------------------------------------------------
    // NFS — in-process driver; requires valid server + export in config
    // -----------------------------------------------------------------------

    [Fact]
    public void For_nfs_without_config_throws_ArgumentException()
    {
        StorageFactory factory = Factory();

        Action act = () => factory.For(Ulid.NewUlid(), "nfs", null, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_nfs_with_valid_config_parses_without_throwing_at_construction()
    {
        // Config parsing succeeds; DllNotFoundException from missing libnfs is
        // expected in this environment and is considered a clean skip scenario.
        StorageFactory factory = Factory();
        string json = """{"server":"nas.local","export":"/media"}""";

        Action act = () => factory.For(Ulid.NewUlid(), "nfs", json, "/irrelevant");

        // Should throw DllNotFoundException (no libnfs installed) but NOT ArgumentException.
        act.Should().NotThrow<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // S3 / R2 — now implemented; null config throws ArgumentException
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s3")]
    [InlineData("r2")]
    public void For_s3_r2_null_config_throws_ArgumentException(string backendType)
    {
        StorageFactory factory = Factory();
        Ulid id = Ulid.NewUlid();

        // null config is invalid — bucket + region are required
        Action act = () => factory.For(id, backendType, null, Path.GetTempPath());

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("s3")]
    public void For_s3_valid_config_returns_RemoteStorage(string backendType)
    {
        StorageFactory factory = Factory();
        Ulid id = Ulid.NewUlid();

        string json =
            $"{{\"bucket\":\"test\",\"region\":\"us-east-1\",\"endpoint\":\"http://localhost:9000\"}}";

        IStorage storage = factory.For(id, backendType, json, Path.GetTempPath());

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_r2_without_endpoint_throws_ArgumentException()
    {
        StorageFactory factory = Factory();
        Ulid id = Ulid.NewUlid();

        string json = "{\"bucket\":\"test\",\"region\":\"auto\"}";
        Action act = () => factory.For(id, "r2", json, Path.GetTempPath());

        act.Should().Throw<ArgumentException>().WithMessage("*endpoint*");
    }

    // -----------------------------------------------------------------------
    // Unknown backend type
    // -----------------------------------------------------------------------

    [Fact]
    public void For_unknown_type_throws_ArgumentException()
    {
        StorageFactory factory = Factory();
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(id, "ftp", null, Path.GetTempPath());

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

        IStorage first = factory.For(id, "local", null, root);
        IStorage second = factory.For(id, "local", null, root);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Invalidate_causes_next_call_to_rebuild()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid id = Ulid.NewUlid();

        IStorage first = factory.For(id, "local", null, root);
        factory.Invalidate(id);
        IStorage second = factory.For(id, "local", null, root);

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Invalidate_only_removes_matching_folder()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid idA = Ulid.NewUlid();
        Ulid idB = Ulid.NewUlid();

        IStorage storageA = factory.For(idA, "local", null, root);
        IStorage storageB = factory.For(idB, "local", null, root);

        factory.Invalidate(idA);

        IStorage storageA2 = factory.For(idA, "local", null, root);
        IStorage storageB2 = factory.For(idB, "local", null, root);

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

        IStorage a1 = factory.For(idA, "local", null, root);
        IStorage b1 = factory.For(idB, "local", null, root);

        factory.InvalidateAll();

        IStorage a2 = factory.For(idA, "local", null, root);
        IStorage b2 = factory.For(idB, "local", null, root);

        a2.Should().NotBeSameAs(a1);
        b2.Should().NotBeSameAs(b1);
    }

    // -----------------------------------------------------------------------
    // Cache key includes config hash — different config → new instance
    // -----------------------------------------------------------------------

    [Fact]
    public void For_different_configJson_returns_new_instance()
    {
        StorageFactory factory = Factory();
        string root = Path.GetTempPath();
        Ulid id = Ulid.NewUlid();

        IStorage first = factory.For(id, "local", null, root);
        IStorage second = factory.For(id, "local", "{\"rootPath\": null}", root);

        // Different config hash → different cache slot → different instance
        second.Should().NotBeSameAs(first);
    }

    // -----------------------------------------------------------------------
    // LocalBackendConfig RootPath override
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_with_RootPath_in_config_uses_config_root()
    {
        Mock<IStorageBackend> backend = BackendMock();
        StorageFactory factory = Factory(backend);

        string configRoot = Path.GetTempPath();
        string json = $"{{\"rootPath\": \"{configRoot.Replace("\\", "\\\\")}\"}}";
        Ulid id = Ulid.NewUlid();

        // folderPath is a different dir — the config RootPath should win
        string folderPath = Path.Combine(configRoot, "sub");
        IStorage storage = factory.For(id, "local", json, folderPath);

        // Paths under configRoot should be allowed
        string allowed = Path.Combine(configRoot, "some", "file.bin");
        Action act = () => storage.Exists(allowed);
        act.Should().NotThrow<StoragePathNotAllowedException>();
    }

    [Fact]
    public void For_local_with_malformed_configJson_falls_back_to_folderPath()
    {
        Mock<IStorageBackend> backend = BackendMock();
        StorageFactory factory = Factory(backend);

        string root = Path.Combine(Path.GetTempPath(), "nm-factory-fallback-" + Ulid.NewUlid());
        Ulid id = Ulid.NewUlid();

        // Malformed JSON — factory should warn and fall back to folderPath
        IStorage storage = factory.For(id, "local", "not-valid-json{{{", root);

        // Guard should be scoped to root (falls back), so outside throws
        string outside = Path.GetTempPath();
        Action act = () => storage.Exists(outside);
        act.Should().Throw<StoragePathNotAllowedException>();
    }
}
