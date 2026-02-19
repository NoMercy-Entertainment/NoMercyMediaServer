using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class MemoryCacheConfigurationTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public MemoryCacheConfigurationTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void MemoryCache_HasSizeLimit_Configured()
    {
        IOptions<MemoryCacheOptions> options =
            _factory.Services.GetRequiredService<IOptions<MemoryCacheOptions>>();

        Assert.NotNull(options.Value.SizeLimit);
        Assert.Equal(1024, options.Value.SizeLimit);
    }

    [Fact]
    public void MemoryCache_HasCompactionPercentage_Configured()
    {
        IOptions<MemoryCacheOptions> options =
            _factory.Services.GetRequiredService<IOptions<MemoryCacheOptions>>();

        Assert.Equal(0.25, options.Value.CompactionPercentage);
    }

    [Fact]
    public void MemoryCache_IsResolvable_FromDI()
    {
        IMemoryCache cache = _factory.Services.GetRequiredService<IMemoryCache>();

        Assert.NotNull(cache);
    }

    [Fact]
    public void MemoryCache_AcceptsEntries_WithSize()
    {
        IMemoryCache cache = _factory.Services.GetRequiredService<IMemoryCache>();

        string key = $"test-key-{Guid.NewGuid()}";

        // Entry must be disposed (committed) before it's visible in cache
        using (ICacheEntry entry = cache.CreateEntry(key))
        {
            entry.Value = "test-value";
            entry.Size = 1;
        }

        Assert.True(cache.TryGetValue(key, out object? value));
        Assert.Equal("test-value", value);

        // Clean up
        cache.Remove(key);
    }
}
