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
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: "speed-index-test-" + Ulid.NewUlid());
        Directory.CreateDirectory(path: _tempDir);
        _storage = TestStorageFactory.CreateLocal();
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    private JsonSpeedIndexStore BuildStore(string? cachePath = null)
    {
        EncoderOptions opts = new()
        {
            SpeedIndexCachePath = cachePath ?? Path.Combine(path1: _tempDir, path2: "speed-index.json"),
        };
        return new(options: opts, logger: NullLogger<JsonSpeedIndexStore>.Instance, storage: _storage);
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
            cachePath: Path.Combine(path1: _tempDir, path2: "does_not_exist.json")
        );

        store.Load().Should().BeNull();
        // No CalibratedAt set on miss.
        store.LastCalibratedAt.Should().BeNull();
    }

    [Fact]
    public void Load_CorruptFile_DegradesToNull()
    {
        string cache = Path.Combine(path1: _tempDir, path2: "corrupt.json");
        File.WriteAllText(path: cache, contents: "{ not valid json");
        JsonSpeedIndexStore store = BuildStore(cachePath: cache);

        store.Load().Should().BeNull();
    }

    [Fact]
    public void Save_NullPath_NoOps()
    {
        // No cache path → save is a no-op; LastCalibratedAt stays null.
        JsonSpeedIndexStore store = BuildStore(cachePath: "");
        SpeedIndex index = new(Measurements: new());

        store.Save(index: index);

        store.LastCalibratedAt.Should().BeNull();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsMeasurements()
    {
        string cache = Path.Combine(path1: _tempDir, path2: "roundtrip.json");
        JsonSpeedIndexStore store = BuildStore(cachePath: cache);
        DateTime measuredAt = new(year: 2026, month: 5, day: 22, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);

        SpeedIndex original = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: "RTX 4080")] = new(
                    Fps: 240.0,
                    SpeedMultiplier: 8.0,
                    MeasuredAt: measuredAt
                ),
                [key: new(Codec: VideoCodecType.H265, Encoder: "libx265", Width: 1280, DeviceName: null)] = new(
                    Fps: 60.0,
                    SpeedMultiplier: 2.0,
                    MeasuredAt: measuredAt
                ),
            }
        );

        store.Save(index: original);

        // Fresh store, same file — exercises the disk persistence layer.
        JsonSpeedIndexStore fresh = BuildStore(cachePath: cache);
        SpeedIndex? loaded = fresh.Load();

        loaded.Should().NotBeNull();
        loaded!.Measurements.Should().HaveCount(expected: 2);
        loaded
            .GetSpeed(codec: VideoCodecType.H264, encoder: "h264_nvenc", width: 1920, deviceName: "RTX 4080")
            .Should()
            .BeEquivalentTo(expectation: new SpeedMeasurement(Fps: 240.0, SpeedMultiplier: 8.0, MeasuredAt: measuredAt));
        loaded
            .GetSpeed(codec: VideoCodecType.H265, encoder: "libx265", width: 1280, deviceName: null)
            .Should()
            .BeEquivalentTo(expectation: new SpeedMeasurement(Fps: 60.0, SpeedMultiplier: 2.0, MeasuredAt: measuredAt));

        // LastCalibratedAt populated on load + matches store-time within
        // the same UTC second.
        fresh.LastCalibratedAt.Should().NotBeNull();
        fresh.LoadedSchemaVersion.Should().Be(expected: HardwareBenchmark.BenchmarkSchemaVersion);
    }

    [Fact]
    public void Save_PopulatesLastCalibratedAtAndSchemaVersion()
    {
        JsonSpeedIndexStore store = BuildStore();
        DateTime before = DateTime.UtcNow;

        store.Save(index: new(Measurements: new()));

        store.LastCalibratedAt.Should().NotBeNull();
        store.LastCalibratedAt!.Value.Should().BeOnOrAfter(expected: before);
        store.LoadedSchemaVersion.Should().Be(expected: HardwareBenchmark.BenchmarkSchemaVersion);
    }

    [Fact]
    public void Save_OverwritesExistingCache()
    {
        string cache = Path.Combine(path1: _tempDir, path2: "overwrite.json");
        JsonSpeedIndexStore store = BuildStore(cachePath: cache);

        // Initial save.
        store.Save(index: new(Measurements: new()));
        store.Save(
            index: new(
                Measurements: new()
                {
                    [key: new(Codec: VideoCodecType.Av1, Encoder: "av1_nvenc", Width: 3840, DeviceName: "RTX 4090")] = new(
                        Fps: 120.0,
                        SpeedMultiplier: 4.0,
                        MeasuredAt: DateTime.UtcNow
                    ),
                }
            )
        );

        SpeedIndex? loaded = store.Load();
        loaded.Should().NotBeNull();
        loaded!.Measurements.Should().HaveCount(expected: 1);
        loaded.GetSpeed(codec: VideoCodecType.Av1, encoder: "av1_nvenc", width: 3840, deviceName: "RTX 4090").Should().NotBeNull();
    }
}
