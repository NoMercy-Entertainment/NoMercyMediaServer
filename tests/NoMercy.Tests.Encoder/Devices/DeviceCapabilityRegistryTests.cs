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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Devices;

namespace NoMercy.Tests.Encoder.Devices;

public class DeviceCapabilityRegistryTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly string _dbName;

    public DeviceCapabilityRegistryTests()
    {
        _dbName = Guid.NewGuid().ToString();
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        _contextFactory = new TestDbContextFactory(options: options);
    }

    public void Dispose() { }

    private static DeviceCapabilities MakeStereoNokiaCaps() =>
        new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac", "ac3"],
            VideoCodecs = ["h264"],
            MaxVideoHeight = 1080,
            RamTier = DeviceRamTier.LowRam,
        };

    [Fact]
    public void Get_ReturnsNull_WhenCacheEmpty()
    {
        DeviceCapabilityRegistry registry = new(contextFactory: _contextFactory);
        DeviceCapabilities? result = registry.Get(deviceId: "unknown-device");
        result.Should().BeNull();
    }

    [Fact]
    public void Set_PopulatesCache_AndGetReturnsIt()
    {
        DeviceCapabilityRegistry registry = new(contextFactory: _contextFactory);
        DeviceCapabilities caps = MakeStereoNokiaCaps();

        registry.Set(deviceId: "nokia-01", capabilities: caps);

        DeviceCapabilities? result = registry.Get(deviceId: "nokia-01");
        result.Should().NotBeNull();
        result!.MaxAudioChannels.Should().Be(expected: 2);
        result.RamTier.Should().Be(expected: DeviceRamTier.LowRam);
    }

    [Fact]
    public void Invalidate_RemovesFromCache()
    {
        DeviceCapabilityRegistry registry = new(contextFactory: _contextFactory);
        registry.Set(deviceId: "nokia-01", capabilities: MakeStereoNokiaCaps());

        registry.Invalidate(deviceId: "nokia-01");

        registry.Get(deviceId: "nokia-01").Should().BeNull();
    }

    [Fact]
    public async Task LoadFromDbAsync_ReadsFromDb_AndCachesResult()
    {
        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device device = new()
        {
            DeviceId = "nokia-db-01",
            Type = "tv",
            CapabilitiesJson = JsonConvert.SerializeObject(value: MakeStereoNokiaCaps()),
        };
        ctx.Devices.Add(entity: device);
        await ctx.SaveChangesAsync();

        DeviceCapabilityRegistry registry = new(contextFactory: _contextFactory);

        DeviceCapabilities? result = await registry.LoadFromDbAsync(
            deviceId: "nokia-db-01",
            ct: CancellationToken.None
        );

        result.Should().NotBeNull();
        result!.MaxAudioChannels.Should().Be(expected: 2);

        // Subsequent Get should hit cache — DB call already populated it
        DeviceCapabilities? cached = registry.Get(deviceId: "nokia-db-01");
        cached.Should().NotBeNull();
        cached!.MaxAudioChannels.Should().Be(expected: 2);
    }

    [Fact]
    public async Task LoadFromDbAsync_ReturnsNull_WhenDeviceNotFound()
    {
        DeviceCapabilityRegistry registry = new(contextFactory: _contextFactory);
        DeviceCapabilities? result = await registry.LoadFromDbAsync(
            deviceId: "missing-device",
            ct: CancellationToken.None
        );
        result.Should().BeNull();
    }

    [Fact]
    public void ConcurrentSetAndGet_DoesNotThrow()
    {
        DeviceCapabilityRegistry registry = new(contextFactory: _contextFactory);
        DeviceCapabilities caps = MakeStereoNokiaCaps();

        Parallel.For(
            fromInclusive: 0,
            toExclusive: 100,
            body: i =>
            {
                string key = $"device-{i % 10}";
                registry.Set(deviceId: key, capabilities: caps);
                registry.Get(deviceId: key);
            }
        );
    }

    private sealed class TestDbContextFactory(DbContextOptions<MediaContext> options)
        : IDbContextFactory<MediaContext>
    {
        public MediaContext CreateDbContext() => new(options: options);

        public Task<MediaContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(result: new MediaContext(options: options));
    }
}
