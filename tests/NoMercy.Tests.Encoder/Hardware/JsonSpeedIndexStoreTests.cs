using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// JsonSpeedIndexStore persists benchmark measurements to disk. Corrupt /
/// missing files must degrade to "no cache" so the benchmark just
/// recalibrates instead of crashing the server. Save + load must round-trip
/// every field so the dashboard sees the same measurements after restart.
/// </summary>
public class JsonSpeedIndexStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalStorage _storage;

    public JsonSpeedIndexStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "speed-index-test-" + Ulid.NewUlid());
        Directory.CreateDirectory(_tempDir);
        _storage = TestStorageFactory.CreateLocal();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private JsonSpeedIndexStore BuildStore(string? cachePath = null)
    {
        EncoderOptions opts = new()
        {
            SpeedIndexCachePath = cachePath ?? Path.Combine(_tempDir, "speed-index.json"),
        };
        return new JsonSpeedIndexStore(opts, NullLogger<JsonSpeedIndexStore>.Instance, _storage);
    }

    [Fact]
    public void Load_NullPath_ReturnsNull()
    {
        JsonSpeedIndexStore store = BuildStore(cachePath: "");

        store.Load().Should().BeNull();
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        JsonSpeedIndexStore store = BuildStore(
            cachePath: Path.Combine(_tempDir, "does_not_exist.json")
        );

        store.Load().Should().BeNull();
        // No CalibratedAt set on miss.
        store.LastCalibratedAt.Should().BeNull();
    }

    [Fact]
    public void Load_CorruptFile_DegradesToNull()
    {
        string cache = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(cache, "{ not valid json");
        JsonSpeedIndexStore store = BuildStore(cache);

        store.Load().Should().BeNull();
    }

    [Fact]
    public void Save_NullPath_NoOps()
    {
        // No cache path → save is a no-op; LastCalibratedAt stays null.
        JsonSpeedIndexStore store = BuildStore(cachePath: "");
        SpeedIndex index = new(new Dictionary<SpeedKey, SpeedMeasurement>());

        store.Save(index);

        store.LastCalibratedAt.Should().BeNull();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsMeasurements()
    {
        string cache = Path.Combine(_tempDir, "roundtrip.json");
        JsonSpeedIndexStore store = BuildStore(cache);
        DateTime measuredAt = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);

        SpeedIndex original = new(
            new Dictionary<SpeedKey, SpeedMeasurement>
            {
                [new SpeedKey(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 4080")] = new(
                    240.0,
                    8.0,
                    measuredAt
                ),
                [new SpeedKey(VideoCodecType.H265, "libx265", 1280, null)] = new(
                    60.0,
                    2.0,
                    measuredAt
                ),
            }
        );

        store.Save(original);

        // Fresh store, same file — exercises the disk persistence layer.
        JsonSpeedIndexStore fresh = BuildStore(cache);
        SpeedIndex? loaded = fresh.Load();

        loaded.Should().NotBeNull();
        loaded!.Measurements.Should().HaveCount(2);
        loaded
            .GetSpeed(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 4080")
            .Should()
            .BeEquivalentTo(new SpeedMeasurement(240.0, 8.0, measuredAt));
        loaded
            .GetSpeed(VideoCodecType.H265, "libx265", 1280, null)
            .Should()
            .BeEquivalentTo(new SpeedMeasurement(60.0, 2.0, measuredAt));

        // LastCalibratedAt populated on load + matches store-time within
        // the same UTC second.
        fresh.LastCalibratedAt.Should().NotBeNull();
        fresh.LoadedSchemaVersion.Should().Be(HardwareBenchmark.BenchmarkSchemaVersion);
    }

    [Fact]
    public void Save_PopulatesLastCalibratedAtAndSchemaVersion()
    {
        JsonSpeedIndexStore store = BuildStore();
        DateTime before = DateTime.UtcNow;

        store.Save(new SpeedIndex(new Dictionary<SpeedKey, SpeedMeasurement>()));

        store.LastCalibratedAt.Should().NotBeNull();
        store.LastCalibratedAt!.Value.Should().BeOnOrAfter(before);
        store.LoadedSchemaVersion.Should().Be(HardwareBenchmark.BenchmarkSchemaVersion);
    }

    [Fact]
    public void Save_OverwritesExistingCache()
    {
        string cache = Path.Combine(_tempDir, "overwrite.json");
        JsonSpeedIndexStore store = BuildStore(cache);

        // Initial save.
        store.Save(new SpeedIndex(new Dictionary<SpeedKey, SpeedMeasurement>()));
        store.Save(
            new SpeedIndex(
                new Dictionary<SpeedKey, SpeedMeasurement>
                {
                    [new SpeedKey(VideoCodecType.Av1, "av1_nvenc", 3840, "RTX 4090")] = new(
                        120.0,
                        4.0,
                        DateTime.UtcNow
                    ),
                }
            )
        );

        SpeedIndex? loaded = store.Load();
        loaded.Should().NotBeNull();
        loaded!.Measurements.Should().HaveCount(1);
        loaded.GetSpeed(VideoCodecType.Av1, "av1_nvenc", 3840, "RTX 4090").Should().NotBeNull();
    }
}
