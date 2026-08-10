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

using System.Runtime.InteropServices;
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
            Path.GetTempPath(),
            "nomercy-lifecycle-mgr-" + Ulid.NewUlid().ToString()
        );
        Directory.CreateDirectory(_tempDir);

        _eventBus = new();
        _registry = new();
        PluginLoader loader = new(
            _eventBus,
            new MinimalServiceProvider(),
            NullLogger.Instance,
            _tempDir,
            TestStorageHelper.CreateStorage(_tempDir),
            _registry,
            new PluginVerifier(),
            new PluginConsentService(new InMemoryConsentStore()),
            TestPluginPlatform.ContextFactory(_eventBus, TestStorageHelper.CreateStorage(_tempDir))
        );

        _lifecycle = new(
            _eventBus,
            new MinimalServiceProvider(),
            NullLogger.Instance,
            _tempDir,
            TestStorageHelper.CreateStorage(_tempDir),
            _registry,
            loader,
            TestPluginPlatform.ContextFactory(_eventBus, TestStorageHelper.CreateStorage(_tempDir))
        );
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
    }

    private static PluginInfo Info(Ulid id, PluginStatus status, string? assemblyPath = null) =>
        new()
        {
            Id = id,
            Name = "Test Plugin",
            Description = "d",
            Version = new(1, 0, 0),
            Status = status,
            AssemblyPath = assemblyPath,
        };

    // ── EnablePluginAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task EnablePluginAsync_UnknownId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _lifecycle.EnablePluginAsync(Ulid.NewUlid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EnablePluginAsync_AlreadyActive_IsANoOp()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active), plugin, null);

        await _lifecycle.EnablePluginAsync(id);

        plugin
            .InitializeCallCount.Should()
            .Be(0, "an already-active plugin must not be re-initialized");
    }

    [Fact]
    public async Task EnablePluginAsync_DisabledWithInstance_InitializesAndTransitionsToActive()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Disabled), plugin, null);
        List<PluginLoadedEvent> loaded = [];
        _eventBus.Subscribe<PluginLoadedEvent>(
            (evt, _) =>
            {
                loaded.Add(evt);
                return Task.CompletedTask;
            }
        );

        await _lifecycle.EnablePluginAsync(id);

        plugin.InitializeCallCount.Should().Be(1);
        _registry.TryGetValue(id, out LoadedPlugin? afterward).Should().BeTrue();
        afterward!.Info.Status.Should().Be(PluginStatus.Active);
        loaded.Should().ContainSingle(e => e.PluginId == id.ToString());
    }

    [Fact]
    public async Task EnablePluginAsync_DisabledWithInstance_CreatesDataFolderWhenMissing()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Disabled), plugin, null);
        string dataFolder = Path.Combine(_tempDir, "data", id.ToString());

        Directory.Exists(dataFolder).Should().BeFalse();

        await _lifecycle.EnablePluginAsync(id);

        Directory.Exists(dataFolder).Should().BeTrue();
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
        Ulid id = Ulid.NewUlid();
        string assemblyPath = Path.Combine(_tempDir, "missing-plugin.dll");
        _registry[id] = new(Info(id, PluginStatus.Disabled, assemblyPath), null, null);

        Func<Task> act = () => _lifecycle.EnablePluginAsync(id);

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
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Deleted), plugin, null);

        Func<Task> act = () => _lifecycle.EnablePluginAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _registry.TryGetValue(id, out LoadedPlugin? afterward).Should().BeTrue();
        afterward!
            .Info.Status.Should()
            .Be(
                PluginStatus.Deleted,
                "the failed transition must not be recorded as Malfunctioned"
            );
    }

    [Fact]
    public async Task EnablePluginAsync_InitializeThrows_MarksMalfunctionedAndPublishesErrorEvent()
    {
        Ulid id = Ulid.NewUlid();
        ThrowingInitializePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Disabled), plugin, null);
        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            (evt, _) =>
            {
                errors.Add(evt);
                return Task.CompletedTask;
            }
        );

        await _lifecycle.EnablePluginAsync(id);

        _registry.TryGetValue(id, out LoadedPlugin? afterward).Should().BeTrue();
        afterward!.Info.Status.Should().Be(PluginStatus.Malfunctioned);
        errors.Should().ContainSingle(e => e.PluginId == id.ToString());
    }

    // ── DisablePluginAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DisablePluginAsync_UnknownId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _lifecycle.DisablePluginAsync(Ulid.NewUlid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisablePluginAsync_AlreadyDisabled_IsANoOp()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Disabled), plugin, null);

        await _lifecycle.DisablePluginAsync(id);

        plugin
            .DisposeCallCount.Should()
            .Be(0, "an already-disabled plugin's instance must not be disposed again");
    }

    [Fact]
    public async Task DisablePluginAsync_Active_DisposesInstanceAndTransitionsToDisabled()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active), plugin, null);

        await _lifecycle.DisablePluginAsync(id);

        plugin.DisposeCallCount.Should().Be(1);
        _registry.TryGetValue(id, out LoadedPlugin? afterward).Should().BeTrue();
        afterward!.Info.Status.Should().Be(PluginStatus.Disabled);
    }

    [Fact]
    public async Task DisableThenEnable_DoesNotReinitializeTheDisposedInstance()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        string assemblyPath = Path.Combine(_tempDir, "missing-plugin.dll");
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath), plugin, null);

        await _lifecycle.DisablePluginAsync(id);

        _registry.TryGetValue(id, out LoadedPlugin? disabled).Should().BeTrue();
        disabled!
            .Instance.Should()
            .BeNull("a disposed instance must not stay in the registry as the live one");

        await _lifecycle.EnablePluginAsync(id);

        plugin
            .InitializeCallCount.Should()
            .Be(
                0,
                "enabling must build a fresh instance, not re-initialize the one that was disposed"
            );
    }

    [Fact]
    public async Task DisablePluginAsync_ActiveWithNullInstance_DoesNotThrow()
    {
        Ulid id = Ulid.NewUlid();
        _registry[id] = new(Info(id, PluginStatus.Active), null, null);

        Func<Task> act = () => _lifecycle.DisablePluginAsync(id);

        await act.Should().NotThrowAsync();
    }

    // ── UninstallPluginAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UninstallPluginAsync_UnknownId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _lifecycle.UninstallPluginAsync(Ulid.NewUlid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UninstallPluginAsync_RemovesFromRegistry_DisposesInstance_TransitionsToDeleted()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active), plugin, null);

        await _lifecycle.UninstallPluginAsync(id);

        _registry.TryGetValue(id, out _).Should().BeFalse();
        plugin.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task UninstallPluginAsync_NoAssemblyPath_DoesNotThrow()
    {
        Ulid id = Ulid.NewUlid();
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath: null), plugin, null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UninstallPluginAsync_AssemblyDirectoryExists_DeletesIt()
    {
        Ulid id = Ulid.NewUlid();
        string pluginDir = Path.Combine(_tempDir, "SomePlugin");
        Directory.CreateDirectory(pluginDir);
        string assemblyPath = Path.Combine(pluginDir, "SomePlugin.dll");
        File.WriteAllBytes(assemblyPath, [1, 2, 3]);
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath), plugin, null);

        await _lifecycle.UninstallPluginAsync(id);

        Directory.Exists(pluginDir).Should().BeFalse();
    }

    [Fact]
    public async Task UninstallPluginAsync_AssemblyDirectoryAlreadyGone_DoesNotThrow()
    {
        Ulid id = Ulid.NewUlid();
        string assemblyPath = Path.Combine(_tempDir, "GoneAlready", "GoneAlready.dll");
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath), plugin, null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UninstallPluginAsync_NullInstance_DoesNotThrow()
    {
        Ulid id = Ulid.NewUlid();
        _registry[id] = new(Info(id, PluginStatus.Active), null, null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(id);

        await act.Should().NotThrowAsync();
        _registry.TryGetValue(id, out _).Should().BeFalse();
    }

    [Fact]
    public async Task UninstallPluginAsync_RealLoadContext_UnloadsIt()
    {
        Ulid id = Ulid.NewUlid();
        string dummyPath = Path.Combine(_tempDir, $"unload-target-{Ulid.NewUlid():N}.dll");
        File.WriteAllBytes(dummyPath, []);
        PluginLoadContext loadContext = new(dummyPath);
        bool unloaded = false;
        loadContext.Unloading += _ => unloaded = true;
        _registry[id] = new(Info(id, PluginStatus.Active), null, loadContext);

        await _lifecycle.UninstallPluginAsync(id);

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
        Ulid id = Ulid.NewUlid();
        string rootPath = Path.DirectorySeparatorChar.ToString();
        Path.GetDirectoryName(rootPath).Should().BeNull("this is exactly the edge case under test");
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath: rootPath), plugin, null);

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(id);

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
        Ulid id = Ulid.NewUlid();
        string pluginDir = Path.Combine(_tempDir, "ReadOnlyPlugin");
        Directory.CreateDirectory(pluginDir);
        string assemblyPath = Path.Combine(pluginDir, "ReadOnlyPlugin.dll");
        string readOnlyFilePath = Path.Combine(pluginDir, "readonly.bin");
        File.WriteAllBytes(assemblyPath, [1, 2, 3]);
        File.WriteAllBytes(readOnlyFilePath, [4, 5, 6]);
        File.SetAttributes(readOnlyFilePath, FileAttributes.ReadOnly);
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath), plugin, null);

        try
        {
            Func<Task> act = () => _lifecycle.UninstallPluginAsync(id);

            await act.Should().NotThrowAsync();
            AssertDeleteBlockedWhereThePlatformBlocksIt(
                pluginDir,
                "the read-only file blocked the recursive delete"
            );
        }
        finally
        {
            // Only Windows keeps the file around: there the read-only flag blocks the
            // delete and the attribute has to be cleared so the fixture can clean up.
            // On POSIX the delete succeeded and the file is already gone, so resetting
            // its attributes throws DirectoryNotFoundException out of the finally.
            if (File.Exists(readOnlyFilePath))
                File.SetAttributes(readOnlyFilePath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task UninstallPluginAsync_DeleteDirectoryFails_LogsWarningInsteadOfThrowing()
    {
        Ulid id = Ulid.NewUlid();
        string pluginDir = Path.Combine(_tempDir, "LockedPlugin");
        Directory.CreateDirectory(pluginDir);
        string assemblyPath = Path.Combine(pluginDir, "LockedPlugin.dll");
        string lockedFilePath = Path.Combine(pluginDir, "locked.bin");
        File.WriteAllBytes(assemblyPath, [1, 2, 3]);
        File.WriteAllBytes(lockedFilePath, [4, 5, 6]);
        FakePlugin plugin = new();
        _registry[id] = new(Info(id, PluginStatus.Active, assemblyPath), plugin, null);

        // Hold an exclusive, non-shared handle open on a file inside the plugin
        // directory for the duration of the delete attempt — the only real way
        // to make Directory.Delete(recursive: true) throw IOException rather
        // than fabricating the exception directly.
        using FileStream lockHandle = new(
            lockedFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        );

        Func<Task> act = () => _lifecycle.UninstallPluginAsync(id);

        await act.Should().NotThrowAsync();
        AssertDeleteBlockedWhereThePlatformBlocksIt(
            pluginDir,
            "the locked file blocked the recursive delete"
        );
    }

    /// <summary>
    /// The contract under test is "uninstall never throws", and that is asserted on
    /// every platform. Whether the directory survives is not portable: a held handle
    /// or a read-only flag only blocks deletion on Windows, while POSIX unlinks open
    /// files and ignores the read-only attribute, so the delete simply succeeds. Both
    /// outcomes are correct — assert the one the running platform actually produces
    /// rather than pinning the Windows result everywhere.
    /// </summary>
    private static void AssertDeleteBlockedWhereThePlatformBlocksIt(
        string pluginDir,
        string because
    )
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Directory.Exists(pluginDir).Should().BeTrue(because);
        else
            Directory
                .Exists(pluginDir)
                .Should()
                .BeFalse("POSIX lets the recursive delete complete regardless");
    }

    private sealed class FakePlugin : IPlugin
    {
        public int InitializeCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public string Name => "fake";
        public string Description => "d";
        public Ulid Id { get; } = Ulid.NewUlid();
        public Version Version { get; } = new(1, 0);

        public void Initialize(IPluginContext context) => InitializeCallCount++;

        public void Dispose() => DisposeCallCount++;
    }

    private sealed class ThrowingInitializePlugin : IPlugin
    {
        public string Name => "throwing";
        public string Description => "d";
        public Ulid Id { get; } = Ulid.NewUlid();
        public Version Version { get; } = new(1, 0);

        public void Initialize(IPluginContext context) =>
            throw new ApplicationException("initialize boom");

        public void Dispose() { }
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
