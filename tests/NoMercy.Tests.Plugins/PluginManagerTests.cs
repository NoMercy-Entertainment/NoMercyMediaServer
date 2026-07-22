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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManagerTests : IDisposable
{
    private readonly string _tempPluginsDir;
    private readonly InMemoryEventBus _eventBus;
    private readonly PluginManager _manager;

    public PluginManagerTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-plugin-tests-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);

        _eventBus = new();
        IServiceProvider services = new MinimalServiceProvider();
        ILogger<PluginManager> logger = NullLogger<PluginManager>.Instance;

        _manager = new(
            eventBus: _eventBus,
            serviceProvider: services,
            logger: logger,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );
    }

    public void Dispose()
    {
        _manager.Dispose();

        try
        {
            if (Directory.Exists(path: _tempPluginsDir))
            {
                Directory.Delete(path: _tempPluginsDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup
        }
    }

    [Fact]
    public void Constructor_NullEventBus_Throws()
    {
        Action act = () =>
            new PluginManager(
                eventBus: null!,
                serviceProvider: new MinimalServiceProvider(),
                logger: NullLogger<PluginManager>.Instance,
                pluginsPath: "/tmp",
                storage: TestStorageHelper.CreateStorage(rootPath: "/tmp"),
                driver: TestStorageHelper.CreateBackend()
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "eventBus");
    }

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        Action act = () =>
            new PluginManager(
                eventBus: new InMemoryEventBus(),
                serviceProvider: null!,
                logger: NullLogger<PluginManager>.Instance,
                pluginsPath: "/tmp",
                storage: TestStorageHelper.CreateStorage(rootPath: "/tmp"),
                driver: TestStorageHelper.CreateBackend()
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "serviceProvider");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Action act = () =>
            new PluginManager(
                eventBus: new InMemoryEventBus(),
                serviceProvider: new MinimalServiceProvider(),
                logger: null!,
                pluginsPath: "/tmp",
                storage: TestStorageHelper.CreateStorage(rootPath: "/tmp"),
                driver: TestStorageHelper.CreateBackend()
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "logger");
    }

    [Fact]
    public void Constructor_NullPluginsPath_Throws()
    {
        Action act = () =>
            new PluginManager(
                eventBus: new InMemoryEventBus(),
                serviceProvider: new MinimalServiceProvider(),
                logger: NullLogger<PluginManager>.Instance,
                pluginsPath: null!,
                storage: TestStorageHelper.CreateStorage(rootPath: "/tmp"),
                driver: TestStorageHelper.CreateBackend()
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "pluginsPath");
    }

    [Fact]
    public void Constructor_NullDriver_Throws()
    {
        Action act = () =>
            new PluginManager(
                eventBus: new InMemoryEventBus(),
                serviceProvider: new MinimalServiceProvider(),
                logger: NullLogger<PluginManager>.Instance,
                pluginsPath: "/tmp",
                storage: TestStorageHelper.CreateStorage(rootPath: "/tmp"),
                driver: null!
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "driver");
    }

    [Fact]
    public void Constructor_NullStorage_Throws()
    {
        Action act = () =>
            new PluginManager(
                eventBus: new InMemoryEventBus(),
                serviceProvider: new MinimalServiceProvider(),
                logger: NullLogger<PluginManager>.Instance,
                pluginsPath: "/tmp",
                storage: null!,
                driver: TestStorageHelper.CreateBackend()
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "storage");
    }

    [Fact]
    public void GetInstalledPlugins_NoPluginsLoaded_ReturnsEmptyList()
    {
        IReadOnlyList<PluginInfo> plugins = _manager.GetInstalledPlugins();

        plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task InstallPluginAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        Func<Task> act = () => _manager.InstallPluginAsync(packagePath: "/nonexistent/path/plugin.dll");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task InstallPluginAsync_NullPath_ThrowsArgumentException()
    {
        Func<Task> act = () => _manager.InstallPluginAsync(packagePath: null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InstallPluginAsync_EmptyPath_ThrowsArgumentException()
    {
        Func<Task> act = () => _manager.InstallPluginAsync(packagePath: "");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnablePluginAsync_UnknownPluginId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _manager.EnablePluginAsync(pluginId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisablePluginAsync_UnknownPluginId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _manager.DisablePluginAsync(pluginId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UninstallPluginAsync_UnknownPluginId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _manager.UninstallPluginAsync(pluginId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_EmptyDirectory_LoadsNothing()
    {
        await _manager.LoadPluginsFromDirectoryAsync();

        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_NonExistentDirectory_DoesNotThrow()
    {
        string nonExistentPath = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nonexistent-" + Guid.NewGuid().ToString(format: "N")
        );
        InMemoryEventBus bus = new();
        PluginManager manager = new(
            eventBus: bus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: nonExistentPath,
            storage: TestStorageHelper.CreateStorage(rootPath: nonExistentPath),
            driver: TestStorageHelper.CreateBackend()
        );

        Func<Task> act = () => manager.LoadPluginsFromDirectoryAsync();

        await act.Should().NotThrowAsync();
        manager.Dispose();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_UnexpectedStorageFailureForOneDirectory_SkipsItAndContinues()
    {
        // "Defense in depth" (see the source comment on this catch): the per-plugin
        // load helpers already isolate their OWN failures internally, so the only
        // way to reach PluginManager's OWN catch here is a fault from the storage
        // layer itself — e.g. mid-scan I/O trouble on one specific directory while
        // the rest of the plugins root is fine. IStorage is PluginManager's
        // collaborator (not the type under test), so substituting a controlled
        // fault for it is the correct way to prove this isolation for real rather
        // than merely by reading the comment.
        Mock<IStorage> storage = new(behavior: MockBehavior.Strict);
        storage.Setup(expression: s => s.Exists(_tempPluginsDir)).Returns(value: true);
        storage
            .Setup(expression: s => s.List(_tempPluginsDir, null, false))
            .Returns(value:
            [
                new StorageEntry(Path: "FaultyPlugin", IsDirectory: true, SizeBytes: 0, LastModified: DateTimeOffset.UtcNow),
                new StorageEntry(Path: "GoodPlugin", IsDirectory: true, SizeBytes: 0, LastModified: DateTimeOffset.UtcNow),
            ]);
        storage.Setup(expression: s => s.Exists(Path.Combine("FaultyPlugin", "plugin.json"))).Returns(value: false);
        storage
            .Setup(expression: s => s.List("FaultyPlugin", "*.dll", false))
            .Throws(exception: new IOException(message: "simulated storage fault enumerating FaultyPlugin"));
        storage.Setup(expression: s => s.Exists(Path.Combine("GoodPlugin", "plugin.json"))).Returns(value: false);
        storage.Setup(expression: s => s.List("GoodPlugin", "*.dll", false)).Returns(value: []);

        PluginManager faultyManager = new(
            eventBus: new InMemoryEventBus(),
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: storage.Object,
            driver: TestStorageHelper.CreateBackend()
        );

        Func<Task> act = () => faultyManager.LoadPluginsFromDirectoryAsync();

        await act.Should()
            .NotThrowAsync(because: "GoodPlugin must still be scanned after FaultyPlugin's failure");
        storage.Verify(expression: s => s.List("GoodPlugin", "*.dll", false), times: Times.Once);
        faultyManager.Dispose();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_StrayFileAtTopLevel_IsSkipped()
    {
        // The top-level listing is non-recursive and includes files alongside
        // plugin subdirectories — a stray file sitting directly in the plugins
        // root (never a valid plugin location) must be skipped via IsDirectory,
        // not treated as a plugin directory name.
        await File.WriteAllTextAsync(path: Path.Combine(path1: _tempPluginsDir, path2: "stray.txt"), contents: "not a plugin");

        Func<Task> act = () => _manager.LoadPluginsFromDirectoryAsync();

        await act.Should().NotThrowAsync();
        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_SkipsConfigurationsAndDataDirs()
    {
        Directory.CreateDirectory(path: Path.Combine(path1: _tempPluginsDir, path2: "configurations"));
        Directory.CreateDirectory(path: Path.Combine(path1: _tempPluginsDir, path2: "data"));

        await _manager.LoadPluginsFromDirectoryAsync();

        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_InvalidDll_PublishesErrorEvent()
    {
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "BadPlugin");
        Directory.CreateDirectory(path: pluginDir);
        string dllPath = Path.Combine(path1: pluginDir, path2: "BadPlugin.dll");
        await File.WriteAllTextAsync(path: dllPath, contents: "not a valid dll");

        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        errors.Should().ContainSingle();
        errors[index: 0].PluginName.Should().Be(expected: "BadPlugin");
        errors[index: 0].ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_InvalidDll_UnloadsContext()
    {
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "BadPlugin2");
        Directory.CreateDirectory(path: pluginDir);
        string dllPath = Path.Combine(path1: pluginDir, path2: "BadPlugin2.dll");
        await File.WriteAllTextAsync(path: dllPath, contents: "garbage data");

        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public void GetPluginInstance_UnknownId_ReturnsNull()
    {
        IPlugin? result = _manager.GetPluginInstance(pluginId: Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public void GetPluginsOfType_NoPlugins_ReturnsEmpty()
    {
        IEnumerable<IMetadataPlugin> result = _manager.GetPluginsOfType<IMetadataPlugin>();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        PluginManager manager = new(
            eventBus: bus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );

        Action act = () =>
        {
            manager.Dispose();
            manager.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void PluginLoadContext_IsCollectible()
    {
        string dummyPath = Path.Combine(path1: _tempPluginsDir, path2: "dummy.dll");
        File.WriteAllBytes(path: dummyPath, bytes: []);

        PluginLoadContext context = new(pluginPath: dummyPath);

        context.IsCollectible.Should().BeTrue();
    }

    [Fact]
    public void PluginContext_StoresAllProperties()
    {
        InMemoryEventBus bus = new();
        MinimalServiceProvider services = new();
        ILogger logger = NullLogger.Instance;
        string dataFolder = _tempPluginsDir;

        PluginContext context = new(
            eventBus: bus,
            services: services,
            logger: logger,
            dataFolderPath: dataFolder,
            storage: TestStorageHelper.CreateStorage(rootPath: dataFolder)
        );

        context.EventBus.Should().BeSameAs(expected: bus);
        context.Services.Should().BeSameAs(expected: services);
        context.Logger.Should().BeSameAs(expected: logger);
        context.DataFolderPath.Should().Be(expected: dataFolder);
    }

    [Fact]
    public void PluginContext_NullEventBus_Throws()
    {
        Action act = () =>
            new PluginContext(
                eventBus: null!,
                services: new MinimalServiceProvider(),
                logger: NullLogger.Instance,
                dataFolderPath: _tempPluginsDir,
                storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir)
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "eventBus");
    }

    [Fact]
    public void PluginContext_NullServices_Throws()
    {
        Action act = () =>
            new PluginContext(
                eventBus: new InMemoryEventBus(),
                services: null!,
                logger: NullLogger.Instance,
                dataFolderPath: _tempPluginsDir,
                storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir)
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "services");
    }

    [Fact]
    public void PluginContext_NullLogger_Throws()
    {
        Action act = () =>
            new PluginContext(
                eventBus: new InMemoryEventBus(),
                services: new MinimalServiceProvider(),
                logger: null!,
                dataFolderPath: _tempPluginsDir,
                storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir)
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "logger");
    }

    [Fact]
    public void PluginContext_NullDataFolder_Throws()
    {
        Action act = () =>
            new PluginContext(
                eventBus: new InMemoryEventBus(),
                services: new MinimalServiceProvider(),
                logger: NullLogger.Instance,
                dataFolderPath: null!,
                storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir)
            );
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "dataFolderPath");
    }

    [Fact]
    public void GetInstalledPlugins_ReturnsReadOnlyList()
    {
        IReadOnlyList<PluginInfo> plugins = _manager.GetInstalledPlugins();

        plugins.Should().BeAssignableTo<IReadOnlyList<PluginInfo>>();
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_MissingAssembly_PublishesErrorEvent()
    {
        Guid pluginId = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "TestPlugin");
        Directory.CreateDirectory(path: pluginDir);

        string manifestJson =
            $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""TestPlugin"",
            ""description"": ""A test"",
            ""version"": ""1.0.0"",
            ""assembly"": ""NonExistent.dll""
        }}";
        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        await File.WriteAllTextAsync(path: manifestPath, contents: manifestJson);

        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        errors.Should().ContainSingle();
        errors[index: 0].PluginName.Should().Be(expected: "TestPlugin");
        errors[index: 0].ErrorMessage.Should().Contain(expected: "NonExistent.dll");
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_InvalidManifest_PublishesErrorEvent()
    {
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "BadManifest");
        Directory.CreateDirectory(path: pluginDir);

        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        await File.WriteAllTextAsync(path: manifestPath, contents: "not valid json");

        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        errors.Should().ContainSingle();
        errors[index: 0].PluginName.Should().Be(expected: "BadManifest");
        errors[index: 0].ErrorMessage.Should().Contain(expected: "Invalid plugin manifest");
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_InvalidDll_PublishesErrorEvent()
    {
        Guid pluginId = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "BadDll");
        Directory.CreateDirectory(path: pluginDir);

        string dllPath = Path.Combine(path1: pluginDir, path2: "BadDll.dll");
        await File.WriteAllTextAsync(path: dllPath, contents: "not a valid dll");

        string manifestJson =
            $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""BadDll"",
            ""description"": ""A test"",
            ""version"": ""1.0.0"",
            ""assembly"": ""BadDll.dll""
        }}";
        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        await File.WriteAllTextAsync(path: manifestPath, contents: manifestJson);

        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        errors.Should().ContainSingle();
        errors[index: 0].PluginName.Should().Be(expected: "BadDll");
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_PrefersManifestOverDllScan()
    {
        Guid pluginId = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "ManifestPlugin");
        Directory.CreateDirectory(path: pluginDir);

        string dllPath = Path.Combine(path1: pluginDir, path2: "ManifestPlugin.dll");
        await File.WriteAllTextAsync(path: dllPath, contents: "garbage data");

        string manifestJson =
            $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""ManifestPlugin"",
            ""description"": ""Uses manifest"",
            ""version"": ""1.0.0"",
            ""assembly"": ""ManifestPlugin.dll""
        }}";
        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        await File.WriteAllTextAsync(path: manifestPath, contents: manifestJson);

        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _manager.LoadPluginsFromDirectoryAsync();

        errors.Should().ContainSingle();
        errors[index: 0].PluginName.Should().Be(expected: "ManifestPlugin");
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
