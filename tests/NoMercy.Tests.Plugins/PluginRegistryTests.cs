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
using System.Collections.Concurrent;
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
            Version = new(major: 1, minor: 0, build: 0),
            Status = PluginStatus.Active,
        };
        return new(info: info, instance: null, loadContext: null);
    }

    [Fact]
    public void Set_TryGetValue_And_Values_RoundTrip()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        LoadedPlugin plugin = MakePlugin(id: id);

        registry[id: id] = plugin;

        registry.TryGetValue(id: id, plugin: out LoadedPlugin? found).Should().BeTrue();
        found.Should().BeSameAs(expected: plugin);
        registry.Values.Should().ContainSingle().Which.Should().BeSameAs(expected: plugin);
    }

    [Fact]
    public void TryRemove_RemovesThePlugin()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        registry[id: id] = MakePlugin(id: id);

        registry.TryRemove(id: id, plugin: out LoadedPlugin? removed).Should().BeTrue();
        removed.Should().NotBeNull();
        registry.TryGetValue(id: id, plugin: out _).Should().BeFalse();
        registry.Values.Should().BeEmpty();
    }

    [Fact]
    public void TryGetValue_UnknownId_ReturnsFalse()
    {
        PluginRegistry registry = new();

        registry.TryGetValue(id: Guid.NewGuid(), plugin: out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        PluginRegistry registry = new();
        registry[id: Guid.NewGuid()] = MakePlugin(id: Guid.NewGuid());
        registry[id: Guid.NewGuid()] = MakePlugin(id: Guid.NewGuid());

        registry.Clear();

        registry.Values.Should().BeEmpty();
    }

    // ── Dispose+unload on replace ────────────────────────────────────────────
    //
    // Every registry write in PluginLoader (manifest reload, direct-assembly
    // reload, malfunctioned re-record) used to overwrite the previous entry
    // outright: its Instance was never disposed and its LoadContext was never
    // unloaded, leaking a collectible ALC that pins the old assembly file.

    [Fact]
    public void Set_ReplacingExistingEntry_DisposesOldInstanceAndUnloadsOldLoadContext()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();

        DisposalTrackingPlugin oldInstance = new();
        string dummyPath = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nm-registry-test-{Guid.NewGuid():N}.dll"
        );
        File.WriteAllBytes(path: dummyPath, bytes: []);
        PluginLoadContext oldContext = new(pluginPath: dummyPath);
        bool oldContextUnloaded = false;
        oldContext.Unloading += _ => oldContextUnloaded = true;

        registry[id: id] = new(info: MakeInfo(id: id), instance: oldInstance, loadContext: oldContext);

        LoadedPlugin newLoaded = new(info: MakeInfo(id: id), instance: new DisposalTrackingPlugin(), loadContext: null);
        registry[id: id] = newLoaded;

        oldInstance.WasDisposed.Should().BeTrue();
        oldContextUnloaded.Should().BeTrue();
        registry.TryGetValue(id: id, plugin: out LoadedPlugin? current).Should().BeTrue();
        current.Should().BeSameAs(expected: newLoaded);

        try
        {
            File.Delete(path: dummyPath);
        }
        catch
        {
            // Best-effort — the ALC may still hold the file briefly post-unload.
        }
    }

    [Fact]
    public void Set_ReplacingWithSameReference_DoesNotDisposeIt()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        DisposalTrackingPlugin instance = new();
        LoadedPlugin loaded = new(info: MakeInfo(id: id), instance: instance, loadContext: null);

        registry[id: id] = loaded;
        registry[id: id] = loaded; // idempotent re-set of the exact same object

        instance.WasDisposed.Should().BeFalse();
    }

    [Fact]
    public void Set_ReplacingEntryWithNullInstance_DoesNotThrow()
    {
        // A Malfunctioned entry recorded with no instance (e.g. a plugin whose
        // constructor threw) has Instance == null — `replaced.Instance?.Dispose()`
        // exists specifically so replacing THAT entry never calls Dispose on a
        // null reference.
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        registry[id: id] = new(info: MakeInfo(id: id), instance: null, loadContext: null);

        Action act = () => registry[id: id] = new(info: MakeInfo(id: id), instance: new DisposalTrackingPlugin(), loadContext: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Set_ConcurrentReplacements_DisposesEverySupersededEntryExactlyOnce()
    {
        PluginRegistry registry = new();
        Guid id = Guid.NewGuid();
        const int concurrentWriters = 50;
        ConcurrentBag<DisposalTrackingPlugin> instances = new();

        Parallel.For(
            fromInclusive: 0,
            toExclusive: concurrentWriters,
            body: i =>
            {
                DisposalTrackingPlugin instance = new();
                instances.Add(item: instance);
                registry[id: id] = new(info: MakeInfo(id: id), instance: instance, loadContext: null);
            }
        );

        int disposedCount = instances.Count(predicate: i => i.WasDisposed);
        disposedCount
            .Should()
            .Be(
                expected: concurrentWriters - 1,
                because: "every superseded entry must be disposed exactly once, and the surviving one must not be"
            );

        registry.TryGetValue(id: id, plugin: out LoadedPlugin? current).Should().BeTrue();
        DisposalTrackingPlugin survivor = (DisposalTrackingPlugin)current!.Instance!;
        survivor.WasDisposed.Should().BeFalse();
    }

    private static PluginInfo MakeInfo(Guid id) =>
        new()
        {
            Id = id,
            Name = "Test",
            Description = string.Empty,
            Version = new(major: 1, minor: 0, build: 0),
            Status = PluginStatus.Active,
        };

    private sealed class DisposalTrackingPlugin : IPlugin
    {
        public bool WasDisposed { get; private set; }

        public string Name => "DisposalTracker";
        public string Description => string.Empty;
        public Guid Id => Guid.NewGuid();
        public Version Version => new(major: 1, minor: 0, build: 0);

        public void Initialize(IPluginContext context) { }

        public void Dispose() => WasDisposed = true;
    }
}
