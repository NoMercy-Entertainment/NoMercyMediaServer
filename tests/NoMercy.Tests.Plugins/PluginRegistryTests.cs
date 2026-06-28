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
using FluentAssertions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginRegistryTests
{
    private static LoadedPlugin MakePlugin(Guid id)
    {
        PluginInfo info = new()
        {
            Id = id,
            Name = "Test",
            Description = string.Empty,
            Version = new Version(1, 0, 0),
            Status = PluginStatus.Active,
        };
        return new LoadedPlugin(info, null, null);
    }

    [Fact]
    public void Set_TryGetValue_And_Values_RoundTrip()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        LoadedPlugin plugin = MakePlugin(id);

        registry[id] = plugin;

        registry.TryGetValue(id, out LoadedPlugin? found).Should().BeTrue();
        found.Should().BeSameAs(plugin);
        registry.Values.Should().ContainSingle().Which.Should().BeSameAs(plugin);
    }

    [Fact]
    public void TryRemove_RemovesThePlugin()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        registry[id] = MakePlugin(id);

        registry.TryRemove(id, out LoadedPlugin? removed).Should().BeTrue();
        removed.Should().NotBeNull();
        registry.TryGetValue(id, out _).Should().BeFalse();
        registry.Values.Should().BeEmpty();
    }

    [Fact]
    public void TryGetValue_UnknownId_ReturnsFalse()
    {
        PluginRegistry registry = new();

        registry.TryGetValue(Guid.NewGuid(), out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        PluginRegistry registry = new();
        registry[Guid.NewGuid()] = MakePlugin(Guid.NewGuid());
        registry[Guid.NewGuid()] = MakePlugin(Guid.NewGuid());

        registry.Clear();

        registry.Values.Should().BeEmpty();
    }
}
