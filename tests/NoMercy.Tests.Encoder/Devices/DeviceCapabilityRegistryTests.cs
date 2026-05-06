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
            .UseInMemoryDatabase(_dbName)
            .Options;
        _contextFactory = new TestDbContextFactory(options);
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
        DeviceCapabilityRegistry registry = new(_contextFactory);
        DeviceCapabilities? result = registry.Get("unknown-device");
        result.Should().BeNull();
    }

    [Fact]
    public void Set_PopulatesCache_AndGetReturnsIt()
    {
        DeviceCapabilityRegistry registry = new(_contextFactory);
        DeviceCapabilities caps = MakeStereoNokiaCaps();

        registry.Set("nokia-01", caps);

        DeviceCapabilities? result = registry.Get("nokia-01");
        result.Should().NotBeNull();
        result!.MaxAudioChannels.Should().Be(2);
        result.RamTier.Should().Be(DeviceRamTier.LowRam);
    }

    [Fact]
    public void Invalidate_RemovesFromCache()
    {
        DeviceCapabilityRegistry registry = new(_contextFactory);
        registry.Set("nokia-01", MakeStereoNokiaCaps());

        registry.Invalidate("nokia-01");

        registry.Get("nokia-01").Should().BeNull();
    }

    [Fact]
    public async Task LoadFromDbAsync_ReadsFromDb_AndCachesResult()
    {
        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device device = new()
        {
            DeviceId = "nokia-db-01",
            Type = "tv",
            CapabilitiesJson = JsonConvert.SerializeObject(MakeStereoNokiaCaps()),
        };
        ctx.Devices.Add(device);
        await ctx.SaveChangesAsync();

        DeviceCapabilityRegistry registry = new(_contextFactory);

        DeviceCapabilities? result = await registry.LoadFromDbAsync(
            "nokia-db-01",
            CancellationToken.None
        );

        result.Should().NotBeNull();
        result!.MaxAudioChannels.Should().Be(2);

        // Subsequent Get should hit cache — DB call already populated it
        DeviceCapabilities? cached = registry.Get("nokia-db-01");
        cached.Should().NotBeNull();
        cached!.MaxAudioChannels.Should().Be(2);
    }

    [Fact]
    public async Task LoadFromDbAsync_ReturnsNull_WhenDeviceNotFound()
    {
        DeviceCapabilityRegistry registry = new(_contextFactory);
        DeviceCapabilities? result = await registry.LoadFromDbAsync(
            "missing-device",
            CancellationToken.None
        );
        result.Should().BeNull();
    }

    [Fact]
    public void ConcurrentSetAndGet_DoesNotThrow()
    {
        DeviceCapabilityRegistry registry = new(_contextFactory);
        DeviceCapabilities caps = MakeStereoNokiaCaps();

        Parallel.For(
            0,
            100,
            i =>
            {
                string key = $"device-{i % 10}";
                registry.Set(key, caps);
                registry.Get(key);
            }
        );
    }

    private sealed class TestDbContextFactory(DbContextOptions<MediaContext> options)
        : IDbContextFactory<MediaContext>
    {
        public MediaContext CreateDbContext() => new(options);

        public Task<MediaContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(new MediaContext(options));
    }
}
