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

        SafePluginIdentity identity = SafePluginIdentity.Read(instance: plugin, pluginType: plugin.GetType());

        identity.Id.Should().Be(expected: WellBehavedPlugin.FixedId);
        identity.Name.Should().Be(expected: "Well Behaved");
        identity.Description.Should().Be(expected: "A normal plugin");
        identity.Version.Should().Be(expected: new(major: 1, minor: 2, build: 3));
    }

    [Fact]
    public void Read_NullInstance_FallsBackToTypeName()
    {
        SafePluginIdentity identity = SafePluginIdentity.Read(instance: null, pluginType: typeof(WellBehavedPlugin));

        identity.Id.Should().Be(expected: Guid.Empty);
        identity.Name.Should().Be(expected: typeof(WellBehavedPlugin).FullName);
        identity.Description.Should().BeEmpty();
        identity.Version.Should().Be(expected: new(major: 0, minor: 0, build: 0));
    }

    [Fact]
    public void Read_ThrowingGetters_FallsBackToSafeDefaultsAndDoesNotThrow()
    {
        ThrowingPlugin plugin = new();

        Func<SafePluginIdentity> act = () => SafePluginIdentity.Read(instance: plugin, pluginType: plugin.GetType());

        SafePluginIdentity identity = act.Should().NotThrow().Subject;
        identity.Id.Should().Be(expected: Guid.Empty);
        identity.Name.Should().Be(expected: typeof(ThrowingPlugin).FullName);
        identity.Description.Should().BeEmpty();
        identity.Version.Should().Be(expected: new(major: 0, minor: 0, build: 0));
    }

    [Fact]
    public void Read_OpenGenericTypeParameter_FullNameIsNull_FallsBackToTypeName()
    {
        // Type.FullName is null for an open generic type parameter — the only
        // real (not fabricated) way to reach the `pluginType.FullName ?? name`
        // fallback, since every concrete, non-generic plugin type discovered
        // via Assembly.GetTypes() always has a non-null FullName.
        Type openGenericParameter = typeof(List<>).GetGenericArguments()[0];
        openGenericParameter.FullName.Should().BeNull(because: "this is exactly the edge case under test");

        SafePluginIdentity identity = SafePluginIdentity.Read(instance: null, pluginType: openGenericParameter);

        identity.Name.Should().Be(expected: openGenericParameter.Name);
    }

    [Fact]
    public void Read_GettersReturnNullWithoutThrowing_FallsBackToSafeDefaults()
    {
        // Distinct from ThrowingPlugin above: these getters succeed (no
        // exception at all) but hand back null despite IPlugin's non-nullable
        // signature — exercising the `instance.Description ?? string.Empty`
        // and `instance.Version ?? UnknownVersion` right-hand sides, which a
        // throwing getter can never reach (the throw happens before the `??`
        // is ever evaluated).
        NullReturningPlugin plugin = new();

        SafePluginIdentity identity = SafePluginIdentity.Read(instance: plugin, pluginType: plugin.GetType());

        identity.Id.Should().Be(expected: NullReturningPlugin.FixedId);
        identity.Name.Should().Be(expected: "Null Returning");
        identity.Description.Should().BeEmpty();
        identity.Version.Should().Be(expected: new(major: 0, minor: 0, build: 0));
    }

    private sealed class WellBehavedPlugin : IPlugin
    {
        public static readonly Guid FixedId = Guid.Parse(input: "11111111-1111-1111-1111-111111111111");

        public string Name => "Well Behaved";
        public string Description => "A normal plugin";
        public Guid Id => FixedId;
        public Version Version => new(major: 1, minor: 2, build: 3);

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }
    }

    private sealed class ThrowingPlugin : IPlugin
    {
        public string Name => throw new InvalidOperationException(message: "name boom");
        public string Description => throw new InvalidOperationException(message: "description boom");
        public Guid Id => throw new InvalidOperationException(message: "id boom");
        public Version Version => throw new InvalidOperationException(message: "version boom");

        public void Initialize(IPluginContext context) =>
            throw new InvalidOperationException(message: "init boom");

        public void Dispose() => throw new InvalidOperationException(message: "dispose boom");
    }

    private sealed class NullReturningPlugin : IPlugin
    {
        public static readonly Guid FixedId = Guid.Parse(input: "44444444-4444-4444-4444-444444444444");

        public string Name => "Null Returning";
        public string Description => null!;
        public Guid Id => FixedId;
        public Version Version => null!;

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }
    }
}
