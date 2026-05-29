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
        _tempDir = Path.Combine(Path.GetTempPath(), "db-fp-test-" + Ulid.NewUlid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static IDbContextFactory<AppDbContext> InMemoryFactory(string dbName)
    {
        DbContextOptions<AppDbContext> opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Mock<IDbContextFactory<AppDbContext>> mock = new();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(opts));
        return mock.Object;
    }

    private DbDriverFingerprintStore BuildStore(
        IDbContextFactory<AppDbContext> factory,
        IStorage? storage = null
    )
    {
        EncoderOptions opts = new()
        {
            SpeedIndexCachePath = Path.Combine(_tempDir, "speed_index.json"),
        };
        return new DbDriverFingerprintStore(
            opts,
            NullLogger<DbDriverFingerprintStore>.Instance,
            storage ?? Mock.Of<IStorage>(),
            factory
        );
    }

    [Fact]
    public async Task LoadHashAsync_NoRow_NoLegacyFile_ReturnsNull()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("load-null-" + Ulid.NewUlid());
        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        DbDriverFingerprintStore store = BuildStore(factory, storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task SaveHashAsync_ThenLoad_RoundTrips()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("rt-" + Ulid.NewUlid());
        DbDriverFingerprintStore store = BuildStore(factory);

        await store.SaveHashAsync("sha256:test-hash");
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be("sha256:test-hash");
    }

    [Fact]
    public async Task SaveHashAsync_Twice_LatestValueWins()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("overwrite-" + Ulid.NewUlid());
        DbDriverFingerprintStore store = BuildStore(factory);

        await store.SaveHashAsync("first");
        await store.SaveHashAsync("second");
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be("second");
    }

    [Fact]
    public async Task LoadHashAsync_LegacyJsonPresent_ImportsAndReturns()
    {
        // No DB row, but legacy file present — store should import the JSON,
        // persist it to AppDbContext.Configuration, then delete the file.
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("legacy-" + Ulid.NewUlid());
        string legacyPath = Path.Combine(_tempDir, "driver_fingerprint.json");
        byte[] legacyContent = Encoding.UTF8.GetBytes(
            JsonConvert.SerializeObject(new { hash = "legacy-hash-from-file" })
        );

        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(legacyPath)).Returns(true);
        storage.Setup(s => s.Read(legacyPath)).Returns(legacyContent);
        storage.Setup(s => s.Delete(legacyPath));

        DbDriverFingerprintStore store = BuildStore(factory, storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().Be("legacy-hash-from-file");
        // Legacy file must be deleted after successful import.
        storage.Verify(s => s.Delete(legacyPath), Times.Once);
    }

    [Fact]
    public async Task LoadHashAsync_LegacyJsonImported_PersistsToDb()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory(
            "legacy-persist-" + Ulid.NewUlid()
        );
        string legacyPath = Path.Combine(_tempDir, "driver_fingerprint.json");
        byte[] legacyContent = Encoding.UTF8.GetBytes(
            JsonConvert.SerializeObject(new { hash = "persisted-from-legacy" })
        );

        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(legacyPath)).Returns(true);
        storage.Setup(s => s.Read(legacyPath)).Returns(legacyContent);

        DbDriverFingerprintStore store = BuildStore(factory, storage.Object);
        await store.LoadHashAsync();

        // Subsequent load should hit the DB row, not the file.
        Mock<IStorage> noLegacy = new();
        noLegacy.Setup(s => s.Exists(legacyPath)).Returns(false);
        DbDriverFingerprintStore fresh = BuildStore(factory, noLegacy.Object);
        string? second = await fresh.LoadHashAsync();

        second.Should().Be("persisted-from-legacy");
    }

    [Fact]
    public async Task LoadHashAsync_CorruptLegacyJson_ReturnsNull()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("corrupt-" + Ulid.NewUlid());
        string legacyPath = Path.Combine(_tempDir, "driver_fingerprint.json");

        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(legacyPath)).Returns(true);
        storage.Setup(s => s.Read(legacyPath)).Returns(Encoding.UTF8.GetBytes("{ not valid"));

        DbDriverFingerprintStore store = BuildStore(factory, storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
        // Corrupt file is NOT deleted — operator can inspect.
        storage.Verify(s => s.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadHashAsync_LegacyJsonEmptyHash_ReturnsNull()
    {
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("empty-" + Ulid.NewUlid());
        string legacyPath = Path.Combine(_tempDir, "driver_fingerprint.json");
        byte[] content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { hash = "" }));

        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(legacyPath)).Returns(true);
        storage.Setup(s => s.Read(legacyPath)).Returns(content);

        DbDriverFingerprintStore store = BuildStore(factory, storage.Object);
        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task LoadHashAsync_DbRowPresent_SkipsLegacyImport()
    {
        // When a DB row exists, the legacy file isn't even consulted —
        // verify Exists/Read are never called.
        IDbContextFactory<AppDbContext> factory = InMemoryFactory("skip-legacy-" + Ulid.NewUlid());
        DbDriverFingerprintStore writer = BuildStore(factory);
        await writer.SaveHashAsync("db-row-hash");

        Mock<IStorage> storage = new(MockBehavior.Strict);
        DbDriverFingerprintStore reader = BuildStore(factory, storage.Object);
        string? hash = await reader.LoadHashAsync();

        hash.Should().Be("db-row-hash");
        // Strict mock: any unexpected call fails the test, so the legacy
        // path being skipped is implicitly verified.
    }
}
