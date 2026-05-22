using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// JsonDriverFingerprintStore persists the driver-fingerprint hash next to
/// the SpeedIndex cache. Corrupt / missing files must degrade to "no
/// previous hash" so the caller treats the next boot as a first boot rather
/// than crashing.
/// </summary>
public class JsonDriverFingerprintStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalStorage _storage;

    public JsonDriverFingerprintStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fp-test-" + Ulid.NewUlid());
        Directory.CreateDirectory(_tempDir);
        _storage = TestStorageFactory.CreateLocal();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private JsonDriverFingerprintStore BuildStore()
    {
        EncoderOptions opts = new()
        {
            SpeedIndexCachePath = Path.Combine(_tempDir, "speed_index.json"),
        };
        return new JsonDriverFingerprintStore(
            opts,
            NullLogger<JsonDriverFingerprintStore>.Instance,
            _storage
        );
    }

    [Fact]
    public async Task LoadHashAsync_MissingFile_ReturnsNull()
    {
        JsonDriverFingerprintStore store = BuildStore();

        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task LoadHashAsync_CorruptFile_DegradesToNull()
    {
        string fpPath = Path.Combine(_tempDir, "driver_fingerprint.json");
        await File.WriteAllTextAsync(fpPath, "{ corrupt");
        JsonDriverFingerprintStore store = BuildStore();

        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task LoadHashAsync_EmptyHashField_ReturnsNull()
    {
        // JSON parses but the hash field is empty — treat as missing so
        // the comparator drives a fresh benchmark.
        string fpPath = Path.Combine(_tempDir, "driver_fingerprint.json");
        await File.WriteAllTextAsync(fpPath, "{\"hash\":\"\"}");
        JsonDriverFingerprintStore store = BuildStore();

        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task SaveHashAsync_ThenLoad_RoundTrips()
    {
        JsonDriverFingerprintStore store = BuildStore();
        const string fingerprint = "sha256:abc123def456";

        await store.SaveHashAsync(fingerprint);
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be(fingerprint);
    }

    [Fact]
    public async Task SaveHashAsync_Overwrites_PreviousHash()
    {
        JsonDriverFingerprintStore store = BuildStore();

        await store.SaveHashAsync("first-hash");
        await store.SaveHashAsync("second-hash");
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be("second-hash");
    }

    [Fact]
    public async Task SaveHashAsync_FileNamedDriverFingerprintJson()
    {
        // Persistence path is derived from SpeedIndexCachePath — the
        // fingerprint sits next to the speed-index cache so admins find
        // both in the same directory.
        JsonDriverFingerprintStore store = BuildStore();

        await store.SaveHashAsync("test-fingerprint");

        File.Exists(Path.Combine(_tempDir, "driver_fingerprint.json")).Should().BeTrue();
    }
}
