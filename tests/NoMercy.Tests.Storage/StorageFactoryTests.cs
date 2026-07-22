// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="StorageFactory"/> tests. Every folder now requires a driver
/// (DriverId is non-nullable). The built-in null-driverId branch is gone.
/// </summary>
public class StorageFactoryTests
{
    private static Mock<IStorageDriver> BackendMock()
    {
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        driver
            .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
        driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);
        return driver;
    }

    private static StorageFactory Factory(
        Mock<IStorageDriver>? driver = null,
        IDriverConfigResolver? resolver = null,
        ICredentialResolver? credentialResolver = null
    )
    {
        Mock<IStorageDriver> b = driver ?? BackendMock();
        return new(driver: b.Object, logger: NullLogger<StorageFactory>.Instance, driverConfigResolver: resolver, credentialResolver: credentialResolver);
    }

    [Fact]
    public void DefaultBuilders_CoverAllBuiltInDriverTypes_WithoutKeyCollisions()
    {
        List<string> keys = StorageFactory
            .DefaultBuilders(driver: BackendMock().Object, logger: NullLogger<StorageFactory>.Instance, credentialResolver: null)
            .SelectMany(selector: builder => builder.SupportedTypes)
            .ToList();

        keys.Should().BeEquivalentTo(expectation: ["local", "nfs", "s3", "r2", "webdav", "smb"]);
        keys.Should().OnlyHaveUniqueItems();
    }

    // Helper: credential resolver stub that always returns a fixed key pair.
    private static ICredentialResolver StubCredentials(
        string accessKey = "test-access-key",
        string secretKey = "test-secret-key"
    )
    {
        Mock<ICredentialResolver> mock = new();
        mock.Setup(expression: r => r.Resolve(It.IsAny<string>())).Returns(value: (accessKey, secretKey));
        return mock.Object;
    }

    // Helper: build a resolver stub that maps any Ulid to a (type, configJson) pair.
    private static IDriverConfigResolver StubResolver(string type, string? config = null)
    {
        Mock<IDriverConfigResolver> mock = new();
        mock.Setup(expression: r => r.Resolve(It.IsAny<Ulid>())).Returns(value: (type, config));
        return mock.Object;
    }

    // Helper: local resolver with a rootPath config pointing at the given directory.
    private static IDriverConfigResolver LocalResolver(string rootPath)
    {
        string escaped = rootPath.Replace(oldValue: "\\", newValue: @"\\");
        string json = $"{{\"rootPath\":\"{escaped}\"}}";
        return StubResolver(type: "local", config: json);
    }

    // -----------------------------------------------------------------------
    // Local driver via resolver
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_returns_IStorage()
    {
        string root = Path.GetTempPath();
        StorageFactory factory = Factory(resolver: LocalResolver(rootPath: root));

        IStorage storage = factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: string.Empty);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    [Fact]
    public void For_local_allows_paths_under_driver_root()
    {
        string root = Path.GetTempPath();
        StorageFactory factory = Factory(resolver: LocalResolver(rootPath: root));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        string inside = Path.Combine(path1: root, path2: "subdir", path3: "file.bin");
        Action act = () => storage.Exists(path: inside);
        act.Should().NotThrow<StoragePathNotAllowedException>();
    }

    [Fact]
    public void For_local_rejects_paths_outside_driver_root()
    {
        Mock<IStorageDriver> driver = BackendMock();
        string root = Path.Combine(path1: Path.GetTempPath(), path2: "nm-factory-test-" + Ulid.NewUlid());
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: root));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        string outside = Path.GetTempPath();
        Action act = () => storage.Exists(path: outside);

        act.Should().Throw<StoragePathNotAllowedException>();
    }

    [Fact]
    public void For_local_null_config_returns_structural_only_guard()
    {
        // Null config = system-local mode (no driver-level rootPath restriction).
        StorageFactory factory = Factory(resolver: StubResolver(type: "local", config: null));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    [Fact]
    public void For_local_empty_rootPath_returns_structural_only_guard()
    {
        // Empty rootPath = system-local mode. No ArgumentException; the factory
        // builds a guard with no allowed roots (structural checks only).
        StorageFactory factory = Factory(resolver: StubResolver(type: "local", config: "{\"rootPath\":\"\"}"));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        storage.Should().NotBeNull().And.BeAssignableTo<IStorage>();
    }

    [Fact]
    public void For_local_empty_rootPath_with_subPath_uses_subPath_as_root()
    {
        // When rootPath is empty but a folder subPath is provided, the subPath
        // becomes the allowed root — so the folder self-constrains.
        Mock<IStorageDriver> driver = BackendMock();
        string folderPath = Path.GetTempPath();
        StorageFactory factory = Factory(driver: driver, resolver: StubResolver(type: "local", config: "{\"rootPath\":\"\"}"));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: folderPath);

        string inside = Path.Combine(path1: folderPath, path2: "movie.mkv");
        Action act = () => storage.Exists(path: inside);
        act.Should().NotThrow<StoragePathNotAllowedException>();
    }

    [Fact]
    public void For_local_with_malformed_configJson_throws_ArgumentException()
    {
        Mock<IStorageDriver> driver = BackendMock();
        string root = Path.Combine(path1: Path.GetTempPath(), path2: "nm-factory-fallback-" + Ulid.NewUlid());
        StorageFactory factory = Factory(driver: driver, resolver: StubResolver(type: "local", config: "not-valid-json{{{"));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: root);

        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // NFS — in-process driver; requires valid server + export in config
    // -----------------------------------------------------------------------

    [Fact]
    public void For_nfs_without_config_throws_ArgumentException()
    {
        StorageFactory factory = Factory(resolver: StubResolver(type: "nfs", config: null));

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_nfs_with_valid_config_parses_without_throwing_at_construction()
    {
        string json = """{"server":"nas.local","export":"/media"}""";
        StorageFactory factory = Factory(resolver: StubResolver(type: "nfs", config: json));

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: "/irrelevant");

        // Should throw DllNotFoundException (no libnfs) but NOT ArgumentException.
        act.Should().NotThrow<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // S3 / R2 — null config throws ArgumentException
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: "s3")]
    [InlineData(data: "r2")]
    public void For_s3_r2_null_config_throws_ArgumentException(string driverType)
    {
        StorageFactory factory = Factory(resolver: StubResolver(type: driverType, config: null));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(data: "s3")]
    public void For_s3_valid_config_returns_RemoteStorage(string driverType)
    {
        string json =
            $"{{\"bucket\":\"test\",\"region\":\"us-east-1\",\"endpoint\":\"http://localhost:9000\"}}";
        StorageFactory factory = Factory(
            resolver: StubResolver(type: driverType, config: json),
            credentialResolver: StubCredentials()
        );
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_r2_without_endpoint_throws_ArgumentException()
    {
        string json = "{\"bucket\":\"test\",\"region\":\"auto\"}";
        StorageFactory factory = Factory(resolver: StubResolver(type: "r2", config: json));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*endpoint*");
    }

    // -----------------------------------------------------------------------
    // WebDAV
    // -----------------------------------------------------------------------

    [Fact]
    public void For_webdav_without_config_throws_ArgumentException()
    {
        StorageFactory factory = Factory(resolver: StubResolver(type: "webdav", config: null));

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_webdav_missing_url_throws_ArgumentException()
    {
        string json = """{"ignoreCertErrors":false}""";
        StorageFactory factory = Factory(resolver: StubResolver(type: "webdav", config: json));

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*url*");
    }

    [Fact]
    public void For_webdav_valid_config_returns_RemoteStorage()
    {
        string json = """{"url":"http://dav.example.com/files/"}""";
        StorageFactory factory = Factory(resolver: StubResolver(type: "webdav", config: json));

        IStorage storage = factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    // -----------------------------------------------------------------------
    // Unknown driver type
    // -----------------------------------------------------------------------

    [Fact]
    public void For_unknown_type_throws_ArgumentException()
    {
        StorageFactory factory = Factory(resolver: StubResolver(type: "ftp"));
        Ulid id = Ulid.NewUlid();

        Action act = () => factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*'ftp'*");
    }

    // -----------------------------------------------------------------------
    // JoinRoot helper
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: ["nfs", "/export", "media", "/export/media"])]
    [InlineData(data: ["s3", "prefix", "sub", "prefix/sub"])]
    [InlineData(data: ["webdav", "http://host/dav/", "movies", "http://host/dav/movies"])]
    public void JoinRoot_combines_root_and_subPath_forward_slash(
        string type,
        string root,
        string sub,
        string expected
    )
    {
        string result = StorageFactory.JoinRoot(root: root, subPath: sub, driverType: type);
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void JoinRoot_local_uses_OS_separator()
    {
        string root = Path.GetTempPath().TrimEnd(trimChar: Path.DirectorySeparatorChar);
        string sub = "movies";
        string result = StorageFactory.JoinRoot(root: root, subPath: sub, driverType: "local");
        result.Should().Be(expected: Path.Combine(path1: root, path2: sub));
    }

    [Fact]
    public void JoinRoot_empty_subPath_returns_root_unchanged()
    {
        string root = Path.GetTempPath();
        string result = StorageFactory.JoinRoot(root: root, subPath: string.Empty, driverType: "local");
        result.Should().Be(expected: root);
    }

    // -----------------------------------------------------------------------
    // Cache semantics
    // -----------------------------------------------------------------------

    [Fact]
    public void For_repeated_call_returns_same_instance()
    {
        string root = Path.GetTempPath();
        StorageFactory factory = Factory(resolver: LocalResolver(rootPath: root));
        Ulid id = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        IStorage first = factory.For(folderId: id, driverId: driverId, subPath: string.Empty);
        IStorage second = factory.For(folderId: id, driverId: driverId, subPath: string.Empty);

        second.Should().BeSameAs(expected: first);
    }

    [Fact]
    public void Invalidate_causes_next_call_to_rebuild()
    {
        string root = Path.GetTempPath();
        StorageFactory factory = Factory(resolver: LocalResolver(rootPath: root));
        Ulid id = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        IStorage first = factory.For(folderId: id, driverId: driverId, subPath: string.Empty);
        factory.Invalidate(folderId: id);
        IStorage second = factory.For(folderId: id, driverId: driverId, subPath: string.Empty);

        second.Should().NotBeSameAs(unexpected: first);
    }

    [Fact]
    public void Invalidate_only_removes_matching_folder()
    {
        string root = Path.GetTempPath();
        StorageFactory factory = Factory(resolver: LocalResolver(rootPath: root));
        Ulid idA = Ulid.NewUlid();
        Ulid idB = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        IStorage storageA = factory.For(folderId: idA, driverId: driverId, subPath: string.Empty);
        IStorage storageB = factory.For(folderId: idB, driverId: driverId, subPath: string.Empty);

        factory.Invalidate(folderId: idA);

        IStorage storageA2 = factory.For(folderId: idA, driverId: driverId, subPath: string.Empty);
        IStorage storageB2 = factory.For(folderId: idB, driverId: driverId, subPath: string.Empty);

        storageA2.Should().NotBeSameAs(unexpected: storageA);
        storageB2.Should().BeSameAs(expected: storageB);
    }

    [Fact]
    public void InvalidateAll_clears_entire_cache()
    {
        string root = Path.GetTempPath();
        StorageFactory factory = Factory(resolver: LocalResolver(rootPath: root));
        Ulid idA = Ulid.NewUlid();
        Ulid idB = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        IStorage a1 = factory.For(folderId: idA, driverId: driverId, subPath: string.Empty);
        IStorage b1 = factory.For(folderId: idB, driverId: driverId, subPath: string.Empty);

        factory.InvalidateAll();

        IStorage a2 = factory.For(folderId: idA, driverId: driverId, subPath: string.Empty);
        IStorage b2 = factory.For(folderId: idB, driverId: driverId, subPath: string.Empty);

        a2.Should().NotBeSameAs(unexpected: a1);
        b2.Should().NotBeSameAs(unexpected: b1);
    }

    // -----------------------------------------------------------------------
    // LocalDriverConfig RootPath override
    // -----------------------------------------------------------------------

    [Fact]
    public void For_local_with_RootPath_in_config_uses_config_root()
    {
        Mock<IStorageDriver> driver = BackendMock();
        string configRoot = Path.GetTempPath();
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: configRoot));
        Ulid id = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        string allowed = Path.Combine(path1: configRoot, path2: "some", path3: "file.bin");
        Action act = () => storage.Exists(path: allowed);
        act.Should().NotThrow<StoragePathNotAllowedException>();
    }

    // -----------------------------------------------------------------------
    // Disposal on eviction / shutdown
    // -----------------------------------------------------------------------
    //
    // Invalidate/InvalidateAll used to drop the cached IStorage without
    // telling its driver to release anything — a real NFS/S3/WebDAV/SMB
    // driver leaks its keep-alive timer, native context, SDK client, or
    // HttpClient every time a folder's storage is invalidated (config
    // change, driver swap, folder delete).

    [Fact]
    public void Invalidate_DisposesUnderlyingDriver_WhenDriverIsDisposable()
    {
        Mock<IStorageDriver> driver = BackendMock();
        driver.As<IDisposable>();
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: Path.GetTempPath()));
        Ulid id = Ulid.NewUlid();
        factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        factory.Invalidate(folderId: id);

        driver.As<IDisposable>().Verify(expression: d => d.Dispose(), times: Times.Once);
    }

    [Fact]
    public void Invalidate_NonDisposableDriver_DoesNotThrow()
    {
        // BackendMock() alone does NOT implement IDisposable — the eviction
        // path must check structurally and no-op, not assume every driver
        // is disposable.
        Mock<IStorageDriver> driver = BackendMock();
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: Path.GetTempPath()));
        Ulid id = Ulid.NewUlid();
        factory.For(folderId: id, driverId: Ulid.NewUlid(), subPath: string.Empty);

        Action act = () => factory.Invalidate(folderId: id);

        act.Should().NotThrow();
    }

    [Fact]
    public void Invalidate_RemovedEntry_NextCallBuildsAndCanDisposeAgain()
    {
        Mock<IStorageDriver> driver = BackendMock();
        driver.As<IDisposable>();
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: Path.GetTempPath()));
        Ulid id = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();
        IStorage first = factory.For(folderId: id, driverId: driverId, subPath: string.Empty);

        factory.Invalidate(folderId: id);
        IStorage second = factory.For(folderId: id, driverId: driverId, subPath: string.Empty);

        second.Should().NotBeSameAs(unexpected: first);
        driver.As<IDisposable>().Verify(expression: d => d.Dispose(), times: Times.Once);
    }

    [Fact]
    public void InvalidateAll_DisposesEveryCachedEntrysDriver()
    {
        Mock<IStorageDriver> driver = BackendMock();
        driver.As<IDisposable>();
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: Path.GetTempPath()));
        factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: string.Empty);
        factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: string.Empty);

        factory.InvalidateAll();

        // Two distinct cache entries (different folder ids) both wrap the
        // same injected local driver — every entry's Dispose() must fire,
        // not just the first one enumerated.
        driver.As<IDisposable>().Verify(expression: d => d.Dispose(), times: Times.Exactly(callCount: 2));
    }

    [Fact]
    public void Dispose_DisposesEveryCachedEntrysDriver()
    {
        Mock<IStorageDriver> driver = BackendMock();
        driver.As<IDisposable>();
        StorageFactory factory = Factory(driver: driver, resolver: LocalResolver(rootPath: Path.GetTempPath()));
        factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: string.Empty);

        factory.Dispose();

        driver.As<IDisposable>().Verify(expression: d => d.Dispose(), times: Times.Once);
    }
}
