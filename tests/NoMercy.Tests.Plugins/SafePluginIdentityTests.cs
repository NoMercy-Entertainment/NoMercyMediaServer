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

public class SafePluginIdentityTests
{
    [Fact]
    public void Read_WellBehavedPlugin_ReturnsActualValues()
    {
        WellBehavedPlugin plugin = new();

        SafePluginIdentity identity = SafePluginIdentity.Read(plugin, plugin.GetType());

        identity.Id.Should().Be(WellBehavedPlugin.FixedId);
        identity.Name.Should().Be("Well Behaved");
        identity.Description.Should().Be("A normal plugin");
        identity.Version.Should().Be(new(1, 2, 3));
    }

    [Fact]
    public void Read_NullInstance_FallsBackToTypeName()
    {
        SafePluginIdentity identity = SafePluginIdentity.Read(null, typeof(WellBehavedPlugin));

        identity.Id.Should().Be(Guid.Empty);
        identity.Name.Should().Be(typeof(WellBehavedPlugin).FullName);
        identity.Description.Should().BeEmpty();
        identity.Version.Should().Be(new(0, 0, 0));
    }

    [Fact]
    public void Read_ThrowingGetters_FallsBackToSafeDefaultsAndDoesNotThrow()
    {
        ThrowingPlugin plugin = new();

        Func<SafePluginIdentity> act = () => SafePluginIdentity.Read(plugin, plugin.GetType());

        SafePluginIdentity identity = act.Should().NotThrow().Subject;
        identity.Id.Should().Be(Guid.Empty);
        identity.Name.Should().Be(typeof(ThrowingPlugin).FullName);
        identity.Description.Should().BeEmpty();
        identity.Version.Should().Be(new(0, 0, 0));
    }

    private sealed class WellBehavedPlugin : IPlugin
    {
        public static readonly Guid FixedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public string Name => "Well Behaved";
        public string Description => "A normal plugin";
        public Guid Id => FixedId;
        public Version Version => new(1, 2, 3);

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }
    }

    private sealed class ThrowingPlugin : IPlugin
    {
        public string Name => throw new InvalidOperationException("name boom");
        public string Description => throw new InvalidOperationException("description boom");
        public Guid Id => throw new InvalidOperationException("id boom");
        public Version Version => throw new InvalidOperationException("version boom");

        public void Initialize(IPluginContext context) =>
            throw new InvalidOperationException("init boom");

        public void Dispose() => throw new InvalidOperationException("dispose boom");
    }
}
