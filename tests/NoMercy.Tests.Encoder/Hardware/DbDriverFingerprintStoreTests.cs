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

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// DbDriverFingerprintStore persists the driver fingerprint hash in
/// AppDbContext.Configuration. Wrong behaviour silently breaks the
/// driver-change detection at boot — the encoder won't know to flush the
/// stale SpeedIndex cache after an NVIDIA driver upgrade.
///
/// Tests use in-memory EF Core to exercise the real persistence path
/// rather than mocking the DbContext.
/// </summary>
public class DbDriverFingerprintStoreTests : IDisposable
{
    private readonly string _tempDir;

    public DbDriverFingerprintStoreTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: "db-fp-test-" + Ulid.NewUlid());
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    private static IDbContextFactory<AppDbContext> InMemoryFactory(string dbName)
    {
        DbContextOptions<AppDbContext> opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        Mock<IDbContextFactory<AppDbContext>> mock = new();
        mock.Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: opts));
        return mock.Object;
    }

    private DbDriverFingerprintStore BuildStore(
        IDbContextFactory<AppDbContext> factory,
        IStorage? storage = null
    )
    {
        EncoderOptions opts = new()
        {
            SpeedIndexCachePath = Path.Combine(path1: _tempDir, path2: "speed_index.json"),
        };
        return new(
            options: opts,
            logger: NullLogger<DbDriverFingerprintStore>.Instance,
            storage: storage ?? Mock.Of<IStorage>(),
            contextFactory: factory
        );
    }

    [Fact]
    public async Task LoadHashAsync_NoRow_NoLegacyFile_ReturnsNull()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "load-null-" + Ulid.NewUlid());
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);

        DbDriverFingerprintStore store = BuildStore(factory: factory, storage: storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task SaveHashAsync_ThenLoad_RoundTrips()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "rt-" + Ulid.NewUlid());
        DbDriverFingerprintStore store = BuildStore(factory: factory);

        await store.SaveHashAsync(hash: "sha256:test-hash");
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be(expected: "sha256:test-hash");
    }

    [Fact]
    public async Task SaveHashAsync_Twice_LatestValueWins()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "overwrite-" + Ulid.NewUlid());
        DbDriverFingerprintStore store = BuildStore(factory: factory);

        await store.SaveHashAsync(hash: "first");
        await store.SaveHashAsync(hash: "second");
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be(expected: "second");
    }

    [Fact]
    public async Task LoadHashAsync_LegacyJsonPresent_ImportsAndReturns()
    {
        // No DB row, but legacy file present — store should import the JSON,
        // persist it to AppDbContext.Configuration, then delete the file.
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "legacy-" + Ulid.NewUlid());
        string legacyPath = Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json");
        byte[] legacyContent = Encoding.UTF8.GetBytes(
            s: JsonConvert.SerializeObject(value: new { hash = "legacy-hash-from-file" })
        );

        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(legacyPath)).Returns(value: true);
        storage.Setup(expression: s => s.Read(legacyPath)).Returns(value: legacyContent);
        storage.Setup(expression: s => s.Delete(legacyPath));

        DbDriverFingerprintStore store = BuildStore(factory: factory, storage: storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().Be(expected: "legacy-hash-from-file");
        // Legacy file must be deleted after successful import.
        storage.Verify(expression: s => s.Delete(legacyPath), times: Times.Once);
    }

    [Fact]
    public async Task LoadHashAsync_LegacyJsonImported_PersistsToDb()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(
            dbName: "legacy-persist-" + Ulid.NewUlid()
        );
        string legacyPath = Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json");
        byte[] legacyContent = Encoding.UTF8.GetBytes(
            s: JsonConvert.SerializeObject(value: new { hash = "persisted-from-legacy" })
        );

        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(legacyPath)).Returns(value: true);
        storage.Setup(expression: s => s.Read(legacyPath)).Returns(value: legacyContent);

        DbDriverFingerprintStore store = BuildStore(factory: factory, storage: storage.Object);
        await store.LoadHashAsync();

        // Subsequent load should hit the DB row, not the file.
        Mock<IStorage> noLegacy = new();
        noLegacy.Setup(expression: s => s.Exists(legacyPath)).Returns(value: false);
        DbDriverFingerprintStore fresh = BuildStore(factory: factory, storage: noLegacy.Object);
        string? second = await fresh.LoadHashAsync();

        second.Should().Be(expected: "persisted-from-legacy");
    }

    [Fact]
    public async Task LoadHashAsync_CorruptLegacyJson_ReturnsNull()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "corrupt-" + Ulid.NewUlid());
        string legacyPath = Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json");

        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(legacyPath)).Returns(value: true);
        storage.Setup(expression: s => s.Read(legacyPath)).Returns(value: Encoding.UTF8.GetBytes(s: "{ not valid"));

        DbDriverFingerprintStore store = BuildStore(factory: factory, storage: storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
        // Corrupt file is NOT deleted — operator can inspect.
        storage.Verify(expression: s => s.Delete(It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public async Task LoadHashAsync_LegacyJsonEmptyHash_ReturnsNull()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "empty-" + Ulid.NewUlid());
        string legacyPath = Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json");
        byte[] content = Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: new { hash = "" }));

        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(legacyPath)).Returns(value: true);
        storage.Setup(expression: s => s.Read(legacyPath)).Returns(value: content);

        DbDriverFingerprintStore store = BuildStore(factory: factory, storage: storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task LoadHashAsync_DbRowPresent_SkipsLegacyImport()
    {
        // When a DB row exists, the legacy file isn't even consulted —
        // verify Exists/Read are never called.
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(dbName: "skip-legacy-" + Ulid.NewUlid());
        DbDriverFingerprintStore writer = BuildStore(factory: factory);
        await writer.SaveHashAsync(hash: "db-row-hash");

        Mock<IStorage> storage = new(behavior: MockBehavior.Strict);
        DbDriverFingerprintStore reader = BuildStore(factory: factory, storage: storage.Object);
        string? hash = await reader.LoadHashAsync();

        hash.Should().Be(expected: "db-row-hash");
        // Strict mock: any unexpected call fails the test, so the legacy
        // path being skipped is implicitly verified.
    }
}
