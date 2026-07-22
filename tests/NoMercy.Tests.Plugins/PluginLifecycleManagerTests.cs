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
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using NoMercy.Plugins.Verification;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Unit-tests <see cref="PluginLifecycleManager"/> directly against a real
/// <see cref="PluginRegistry"/> and real <see cref="PluginLoader"/>, bypassing
/// <see cref="PluginManager"/> entirely. InternalsVisibleTo makes every type
/// here reachable; going through PluginManager's full assembly-loading pipeline
/// for every Enable/Disable/Uninstall branch would require staging a real
/// plugin assembly per scenario for no additional correctness value — the
/// LoadedPlugin objects this class operates on are the real production
/// contract, constructed directly instead of round-tripped through a loader.
/// </summary>
public class PluginLifecycleManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InMemoryEventBus _eventBus;
    private readonly PluginRegistry _registry;
    private readonly PluginLifecycleManager _lifecycle;

    public PluginLifecycleManagerTests()
    {
        _tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-lifecycle-mgr-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempDir);

        _eventBus = new();
        _registry = new();
        PluginLoader loader = new(
            eventBus: _eventBus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger.Instance,
            pluginsPath: _tempDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempDir),
            registry: _registry,
            verifier: new PluginVerifier(),
            consentService: new PluginConsentService(store: new InMemoryConsentStore())
        );

        _lifecycle = new(
            eventBus: _eventBus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger.Instance,
            pluginsPath: _tempDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempDir),
            registry: _registry,
            loader: loader
        );
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(path: _tempDir))
                Directory.Delete(path: _tempDir, recursive: true);
        }
        catch (IOException) { }
    }

    private static PluginInfo Info(Guid id, PluginStatus status, string? assemblyPath = null) =>
        new()
        {
            Id = id,
            Name = "Test Plugin",
            Description = "d",
            Version = new(major: 1, minor: 0, build: 0),
            Status = status,
            AssemblyPath = assemblyPath,
        };

    // ── EnablePluginAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task EnablePluginAsync_UnknownId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _lifecycle.EnablePluginAsync(pluginId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EnablePluginAsync_AlreadyActive_IsANoOp()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active), instance: plugin, loadContext: null);

        await _lifecycle.EnablePluginAsync(pluginId: id);

        plugin
            .InitializeCallCount.Should()
            .Be(expected: 0, because: "an already-active plugin must not be re-initialized");
    }

    [Fact]
    public async Task EnablePluginAsync_DisabledWithInstance_InitializesAndTransitionsToActive()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Disabled), instance: plugin, loadContext: null);
        List<PluginLoadedEvent> loaded = [];
        _eventBus.Subscribe<PluginLoadedEvent>(
            handler: (evt, _) =>
            {
                loaded.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _lifecycle.EnablePluginAsync(pluginId: id);

        plugin.InitializeCallCount.Should().Be(expected: 1);
        _registry.TryGetValue(id: id, plugin: out LoadedPlugin? afterward).Should().BeTrue();
        afterward!.Info.Status.Should().Be(expected: PluginStatus.Active);
        loaded.Should().ContainSingle(predicate: e => e.PluginId == id.ToString());
    }

    [Fact]
    public async Task EnablePluginAsync_DisabledWithInstance_CreatesDataFolderWhenMissing()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Disabled), instance: plugin, loadContext: null);
        string dataFolder = Path.Combine(path1: _tempDir, path2: "data", path3: id.ToString(format: "N"));

        Directory.Exists(path: dataFolder).Should().BeFalse();

        await _lifecycle.EnablePluginAsync(pluginId: id);

        Directory.Exists(path: dataFolder).Should().BeTrue();
    }

    [Fact]
    public async Task EnablePluginAsync_NullInstanceWithAssemblyPath_DelegatesToLoaderAndReturns()
    {
        // No manifest/assembly actually needs to exist on disk for THIS
        // assertion — LoadPluginAssemblyAsync's own load-context-construction
        // catch reports the failure as a PluginErrorOccurredEvent rather than
        // throwing, so EnablePluginAsync completes either way. What this proves
        // is that the null-instance branch defers to the loader instead of
        // trying to call Initialize() on a null reference.
        Guid id = Guid.NewGuid();
        string assemblyPath = Path.Combine(path1: _tempDir, path2: "missing-plugin.dll");
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Disabled, assemblyPath: assemblyPath), instance: null, loadContext: null);

        Func<Task> act = () => _lifecycle.EnablePluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnablePluginAsync_DeletedStatusWithInstance_RethrowsInvalidOperationWithoutMalfunctioning()
    {
        // Deleted is a terminal status — PluginLifecycle.Transition(Deleted, Active)
        // itself throws InvalidOperationException, and EnablePluginAsync's own
        // `catch (InvalidOperationException) { throw; }` must let that specific
        // exception through unmodified rather than recording it as a generic
        // malfunction.
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Deleted), instance: plugin, loadContext: null);

        Func<Task> act = () => _lifecycle.EnablePluginAsync(pluginId: id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _registry.TryGetValue(id: id, plugin: out LoadedPlugin? afterward).Should().BeTrue();
        afterward!
            .Info.Status.Should()
            .Be(
                expected: PluginStatus.Deleted,
                because: "the failed transition must not be recorded as Malfunctioned"
            );
    }

    [Fact]
    public async Task EnablePluginAsync_InitializeThrows_MarksMalfunctionedAndPublishesErrorEvent()
    {
        Guid id = Guid.NewGuid();
        ThrowingInitializePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Disabled), instance: plugin, loadContext: null);
        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _lifecycle.EnablePluginAsync(pluginId: id);

        _registry.TryGetValue(id: id, plugin: out LoadedPlugin? afterward).Should().BeTrue();
        afterward!.Info.Status.Should().Be(expected: PluginStatus.Malfunctioned);
        errors.Should().ContainSingle(predicate: e => e.PluginId == id.ToString());
    }

    // ── DisablePluginAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DisablePluginAsync_UnknownId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _lifecycle.DisablePluginAsync(pluginId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisablePluginAsync_AlreadyDisabled_IsANoOp()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Disabled), instance: plugin, loadContext: null);

        await _lifecycle.DisablePluginAsync(pluginId: id);

        plugin
            .DisposeCallCount.Should()
            .Be(expected: 0, because: "an already-disabled plugin's instance must not be disposed again");
    }

    [Fact]
    public async Task DisablePluginAsync_Active_DisposesInstanceAndTransitionsToDisabled()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active), instance: plugin, loadContext: null);

        await _lifecycle.DisablePluginAsync(pluginId: id);

        plugin.DisposeCallCount.Should().Be(expected: 1);
        _registry.TryGetValue(id: id, plugin: out LoadedPlugin? afterward).Should().BeTrue();
        afterward!.Info.Status.Should().Be(expected: PluginStatus.Disabled);
    }

    [Fact]
    public async Task DisablePluginAsync_ActiveWithNullInstance_DoesNotThrow()
    {
        Guid id = Guid.NewGuid();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active), instance: null, loadContext: null);

        Func<Task> act = () => _lifecycle.DisablePluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
    }

    // ── UninstallPluginAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UninstallPluginAsync_UnknownId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UninstallPluginAsync_RemovesFromRegistry_DisposesInstance_TransitionsToDeleted()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active), instance: plugin, loadContext: null);

        await _lifecycle.UninstallPluginAsync(pluginId: id);

        _registry.TryGetValue(id: id, plugin: out _).Should().BeFalse();
        plugin.DisposeCallCount.Should().Be(expected: 1);
    }

    [Fact]
    public async Task UninstallPluginAsync_NoAssemblyPath_DoesNotThrow()
    {
        Guid id = Guid.NewGuid();
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active, assemblyPath: null), instance: plugin, loadContext: null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UninstallPluginAsync_AssemblyDirectoryExists_DeletesIt()
    {
        Guid id = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempDir, path2: "SomePlugin");
        Directory.CreateDirectory(path: pluginDir);
        string assemblyPath = Path.Combine(path1: pluginDir, path2: "SomePlugin.dll");
        File.WriteAllBytes(path: assemblyPath, bytes: [1, 2, 3]);
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active, assemblyPath: assemblyPath), instance: plugin, loadContext: null);

        await _lifecycle.UninstallPluginAsync(pluginId: id);

        Directory.Exists(path: pluginDir).Should().BeFalse();
    }

    [Fact]
    public async Task UninstallPluginAsync_AssemblyDirectoryAlreadyGone_DoesNotThrow()
    {
        Guid id = Guid.NewGuid();
        string assemblyPath = Path.Combine(path1: _tempDir, path2: "GoneAlready", path3: "GoneAlready.dll");
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active, assemblyPath: assemblyPath), instance: plugin, loadContext: null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UninstallPluginAsync_NullInstance_DoesNotThrow()
    {
        Guid id = Guid.NewGuid();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active), instance: null, loadContext: null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
        _registry.TryGetValue(id: id, plugin: out _).Should().BeFalse();
    }

    [Fact]
    public async Task UninstallPluginAsync_RealLoadContext_UnloadsIt()
    {
        Guid id = Guid.NewGuid();
        string dummyPath = Path.Combine(path1: _tempDir, path2: $"unload-target-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path: dummyPath, bytes: []);
        PluginLoadContext loadContext = new(pluginPath: dummyPath);
        bool unloaded = false;
        loadContext.Unloading += _ => unloaded = true;
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active), instance: null, loadContext: loadContext);

        await _lifecycle.UninstallPluginAsync(pluginId: id);

        unloaded.Should().BeTrue();
    }

    [Fact]
    public async Task UninstallPluginAsync_AssemblyPathIsARootPath_DoesNotThrow()
    {
        // Path.GetDirectoryName returns null ONLY for a bare root path (e.g.
        // the OS directory separator itself) — NOT for a directory-less
        // filename like "bare.dll", which resolves to "" (empty, non-null).
        // The `pluginDir is not null` half of this guard exists specifically
        // for this root-path shape.
        Guid id = Guid.NewGuid();
        string rootPath = Path.DirectorySeparatorChar.ToString();
        Path.GetDirectoryName(path: rootPath).Should().BeNull(because: "this is exactly the edge case under test");
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active, assemblyPath: rootPath), instance: plugin, loadContext: null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UninstallPluginAsync_DeleteDirectoryFailsWithAccessDenied_LogsWarningInsteadOfThrowing()
    {
        // Distinct exception type from the IOException test below: a read-only
        // file inside the directory makes Directory.Delete(recursive: true)
        // throw UnauthorizedAccessException specifically, deterministically —
        // this is what Windows actually throws for a plugin assembly that was
        // just Unload()ed but not yet garbage-collected (a real, non-contrived
        // race during a genuine uninstall), which a bare `catch (IOException)`
        // does not cover.
        Guid id = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempDir, path2: "ReadOnlyPlugin");
        Directory.CreateDirectory(path: pluginDir);
        string assemblyPath = Path.Combine(path1: pluginDir, path2: "ReadOnlyPlugin.dll");
        string readOnlyFilePath = Path.Combine(path1: pluginDir, path2: "readonly.bin");
        File.WriteAllBytes(path: assemblyPath, bytes: [1, 2, 3]);
        File.WriteAllBytes(path: readOnlyFilePath, bytes: [4, 5, 6]);
        File.SetAttributes(path: readOnlyFilePath, fileAttributes: FileAttributes.ReadOnly);
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active, assemblyPath: assemblyPath), instance: plugin, loadContext: null);

        try
        {
            Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: id);

            await act.Should().NotThrowAsync();
            Directory
                .Exists(path: pluginDir)
                .Should()
                .BeTrue(because: "the read-only file blocked the recursive delete");
        }
        finally
        {
            File.SetAttributes(path: readOnlyFilePath, fileAttributes: FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task UninstallPluginAsync_DeleteDirectoryFails_LogsWarningInsteadOfThrowing()
    {
        Guid id = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempDir, path2: "LockedPlugin");
        Directory.CreateDirectory(path: pluginDir);
        string assemblyPath = Path.Combine(path1: pluginDir, path2: "LockedPlugin.dll");
        string lockedFilePath = Path.Combine(path1: pluginDir, path2: "locked.bin");
        File.WriteAllBytes(path: assemblyPath, bytes: [1, 2, 3]);
        File.WriteAllBytes(path: lockedFilePath, bytes: [4, 5, 6]);
        FakePlugin plugin = new();
        _registry[id: id] = new(info: Info(id: id, status: PluginStatus.Active, assemblyPath: assemblyPath), instance: plugin, loadContext: null);

        // Hold an exclusive, non-shared handle open on a file inside the plugin
        // directory for the duration of the delete attempt — the only real way
        // to make Directory.Delete(recursive: true) throw IOException rather
        // than fabricating the exception directly.
        using FileStream lockHandle = new(
            path: lockedFilePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.None
        );

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(pluginId: id);

        await act.Should().NotThrowAsync();
        Directory.Exists(path: pluginDir).Should().BeTrue(because: "the locked file blocked the recursive delete");
    }

    private sealed class FakePlugin : IPlugin
    {
        public int InitializeCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public string Name => "fake";
        public string Description => "d";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0);

        public void Initialize(IPluginContext context) => InitializeCallCount++;

        public void Dispose() => DisposeCallCount++;
    }

    private sealed class ThrowingInitializePlugin : IPlugin
    {
        public string Name => "throwing";
        public string Description => "d";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0);

        public void Initialize(IPluginContext context) =>
            throw new ApplicationException(message: "initialize boom");

        public void Dispose() { }
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
