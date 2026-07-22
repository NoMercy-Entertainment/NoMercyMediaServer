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

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Unit")]
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
        IOptions<MemoryCacheOptions> options = _factory.Services.GetRequiredService<
            IOptions<MemoryCacheOptions>
        >();

        Assert.NotNull(value: options.Value.SizeLimit);
        Assert.Equal(expected: 1024, actual: options.Value.SizeLimit);
    }

    [Fact]
    public void MemoryCache_HasCompactionPercentage_Configured()
    {
        IOptions<MemoryCacheOptions> options = _factory.Services.GetRequiredService<
            IOptions<MemoryCacheOptions>
        >();

        Assert.Equal(expected: 0.25, actual: options.Value.CompactionPercentage);
    }

    [Fact]
    public void MemoryCache_IsResolvable_FromDI()
    {
        IMemoryCache cache = _factory.Services.GetRequiredService<IMemoryCache>();

        Assert.NotNull(@object: cache);
    }

    [Fact]
    public void MemoryCache_AcceptsEntries_WithSize()
    {
        IMemoryCache cache = _factory.Services.GetRequiredService<IMemoryCache>();

        string key = $"test-key-{Guid.NewGuid()}";

        // Entry must be disposed (committed) before it's visible in cache
        using (ICacheEntry entry = cache.CreateEntry(key: key))
        {
            entry.Value = "test-value";
            entry.Size = 1;
        }

        Assert.True(condition: cache.TryGetValue(key: key, value: out object? value));
        Assert.Equal(expected: "test-value", actual: value);

        // Clean up
        cache.Remove(key: key);
    }
}
