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
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: "fp-test-" + Ulid.NewUlid());
        Directory.CreateDirectory(path: _tempDir);
        _storage = TestStorageFactory.CreateLocal();
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    private JsonDriverFingerprintStore BuildStore()
    {
        EncoderOptions opts = new()
        {
            SpeedIndexCachePath = Path.Combine(path1: _tempDir, path2: "speed_index.json"),
        };
        return new(
            options: opts,
            logger: NullLogger<JsonDriverFingerprintStore>.Instance,
            storage: _storage
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
        string fpPath = Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json");
        await File.WriteAllTextAsync(path: fpPath, contents: "{ corrupt");
        JsonDriverFingerprintStore store = BuildStore();

        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task LoadHashAsync_EmptyHashField_ReturnsNull()
    {
        // JSON parses but the hash field is empty — treat as missing so
        // the comparator drives a fresh benchmark.
        string fpPath = Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json");
        await File.WriteAllTextAsync(path: fpPath, contents: "{\"hash\":\"\"}");
        JsonDriverFingerprintStore store = BuildStore();

        string? hash = await store.LoadHashAsync();

        hash.Should().BeNull();
    }

    [Fact]
    public async Task SaveHashAsync_ThenLoad_RoundTrips()
    {
        JsonDriverFingerprintStore store = BuildStore();
        const string fingerprint = "sha256:abc123def456";

        await store.SaveHashAsync(hash: fingerprint);
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be(expected: fingerprint);
    }

    [Fact]
    public async Task SaveHashAsync_Overwrites_PreviousHash()
    {
        JsonDriverFingerprintStore store = BuildStore();

        await store.SaveHashAsync(hash: "first-hash");
        await store.SaveHashAsync(hash: "second-hash");
        string? loaded = await store.LoadHashAsync();

        loaded.Should().Be(expected: "second-hash");
    }

    [Fact]
    public async Task SaveHashAsync_FileNamedDriverFingerprintJson()
    {
        // Persistence path is derived from SpeedIndexCachePath — the
        // fingerprint sits next to the speed-index cache so admins find
        // both in the same directory.
        JsonDriverFingerprintStore store = BuildStore();

        await store.SaveHashAsync(hash: "test-fingerprint");

        File.Exists(path: Path.Combine(path1: _tempDir, path2: "driver_fingerprint.json")).Should().BeTrue();
    }
}
