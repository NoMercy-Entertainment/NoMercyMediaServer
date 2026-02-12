using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManagerTests : IDisposable
{
    private readonly string _tempPluginsDir;
    private readonly InMemoryEventBus _eventBus;
    private readonly PluginManager _manager;

    public PluginManagerTests()
    {
        _tempPluginsDir = Path.Combine(Path.GetTempPath(), "nomercy-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempPluginsDir);

        _eventBus = new InMemoryEventBus();
        IServiceProvider services = new MinimalServiceProvider();
        ILogger<PluginManager> logger = NullLogger<PluginManager>.Instance;

        _manager = new PluginManager(_eventBus, services, logger, _tempPluginsDir);
    }

    public void Dispose()
    {
        _manager.Dispose();

        try
        {
            if (Directory.Exists(_tempPluginsDir))
            {
                Directory.Delete(_tempPluginsDir, recursive: true);
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
        Action act = () => new PluginManager(null!, new MinimalServiceProvider(), NullLogger<PluginManager>.Instance, "/tmp");
        act.Should().Throw<ArgumentNullException>().WithParameterName("eventBus");
    }

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        Action act = () => new PluginManager(new InMemoryEventBus(), null!, NullLogger<PluginManager>.Instance, "/tmp");
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Action act = () => new PluginManager(new InMemoryEventBus(), new MinimalServiceProvider(), null!, "/tmp");
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullPluginsPath_Throws()
    {
        Action act = () => new PluginManager(new InMemoryEventBus(), new MinimalServiceProvider(), NullLogger<PluginManager>.Instance, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pluginsPath");
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
        Func<Task> act = () => _manager.InstallPluginAsync("/nonexistent/path/plugin.dll");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task InstallPluginAsync_NullPath_ThrowsArgumentException()
    {
        Func<Task> act = () => _manager.InstallPluginAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InstallPluginAsync_EmptyPath_ThrowsArgumentException()
    {
        Func<Task> act = () => _manager.InstallPluginAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnablePluginAsync_UnknownPluginId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _manager.EnablePluginAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisablePluginAsync_UnknownPluginId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _manager.DisablePluginAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UninstallPluginAsync_UnknownPluginId_ThrowsInvalidOperation()
    {
        Func<Task> act = () => _manager.UninstallPluginAsync(Guid.NewGuid());

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
        string nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N"));
        InMemoryEventBus bus = new();
        PluginManager manager = new(bus, new MinimalServiceProvider(), NullLogger<PluginManager>.Instance, nonExistentPath);

        Func<Task> act = () => manager.LoadPluginsFromDirectoryAsync();

        await act.Should().NotThrowAsync();
        manager.Dispose();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_SkipsConfigurationsAndDataDirs()
    {
        Directory.CreateDirectory(Path.Combine(_tempPluginsDir, "configurations"));
        Directory.CreateDirectory(Path.Combine(_tempPluginsDir, "data"));

        await _manager.LoadPluginsFromDirectoryAsync();

        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_InvalidDll_PublishesErrorEvent()
    {
        string pluginDir = Path.Combine(_tempPluginsDir, "BadPlugin");
        Directory.CreateDirectory(pluginDir);
        string dllPath = Path.Combine(pluginDir, "BadPlugin.dll");
        await File.WriteAllTextAsync(dllPath, "not a valid dll");

        List<PluginErrorEvent> errors = [];
        _eventBus.Subscribe<PluginErrorEvent>((evt, _) =>
        {
            errors.Add(evt);
            return Task.CompletedTask;
        });

        await _manager.LoadPluginAssemblyAsync(dllPath);

        errors.Should().ContainSingle();
        errors[0].PluginName.Should().Be("BadPlugin");
        errors[0].ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_InvalidDll_UnloadsContext()
    {
        string pluginDir = Path.Combine(_tempPluginsDir, "BadPlugin2");
        Directory.CreateDirectory(pluginDir);
        string dllPath = Path.Combine(pluginDir, "BadPlugin2.dll");
        await File.WriteAllTextAsync(dllPath, "garbage data");

        await _manager.LoadPluginAssemblyAsync(dllPath);

        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public void GetPluginInstance_UnknownId_ReturnsNull()
    {
        IPlugin? result = _manager.GetPluginInstance(Guid.NewGuid());

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
        PluginManager manager = new(bus, new MinimalServiceProvider(), NullLogger<PluginManager>.Instance, _tempPluginsDir);

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
        string dummyPath = Path.Combine(_tempPluginsDir, "dummy.dll");
        File.WriteAllBytes(dummyPath, []);

        PluginLoadContext context = new(dummyPath);

        context.IsCollectible.Should().BeTrue();
    }

    [Fact]
    public void PluginContext_StoresAllProperties()
    {
        InMemoryEventBus bus = new();
        MinimalServiceProvider services = new();
        ILogger logger = NullLogger.Instance;
        string dataFolder = "/tmp/test-data";

        PluginContext context = new(bus, services, logger, dataFolder);

        context.EventBus.Should().BeSameAs(bus);
        context.Services.Should().BeSameAs(services);
        context.Logger.Should().BeSameAs(logger);
        context.DataFolderPath.Should().Be(dataFolder);
    }

    [Fact]
    public void PluginContext_NullEventBus_Throws()
    {
        Action act = () => new PluginContext(null!, new MinimalServiceProvider(), NullLogger.Instance, "/tmp");
        act.Should().Throw<ArgumentNullException>().WithParameterName("eventBus");
    }

    [Fact]
    public void PluginContext_NullServices_Throws()
    {
        Action act = () => new PluginContext(new InMemoryEventBus(), null!, NullLogger.Instance, "/tmp");
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void PluginContext_NullLogger_Throws()
    {
        Action act = () => new PluginContext(new InMemoryEventBus(), new MinimalServiceProvider(), null!, "/tmp");
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void PluginContext_NullDataFolder_Throws()
    {
        Action act = () => new PluginContext(new InMemoryEventBus(), new MinimalServiceProvider(), NullLogger.Instance, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dataFolderPath");
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
        string pluginDir = Path.Combine(_tempPluginsDir, "TestPlugin");
        Directory.CreateDirectory(pluginDir);

        string manifestJson = $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""TestPlugin"",
            ""description"": ""A test"",
            ""version"": ""1.0.0"",
            ""assembly"": ""NonExistent.dll""
        }}";
        string manifestPath = Path.Combine(pluginDir, "plugin.json");
        await File.WriteAllTextAsync(manifestPath, manifestJson);

        List<PluginErrorEvent> errors = [];
        _eventBus.Subscribe<PluginErrorEvent>((evt, _) =>
        {
            errors.Add(evt);
            return Task.CompletedTask;
        });

        await _manager.LoadPluginFromManifestAsync(manifestPath);

        errors.Should().ContainSingle();
        errors[0].PluginName.Should().Be("TestPlugin");
        errors[0].ErrorMessage.Should().Contain("NonExistent.dll");
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_InvalidManifest_PublishesErrorEvent()
    {
        string pluginDir = Path.Combine(_tempPluginsDir, "BadManifest");
        Directory.CreateDirectory(pluginDir);

        string manifestPath = Path.Combine(pluginDir, "plugin.json");
        await File.WriteAllTextAsync(manifestPath, "not valid json");

        List<PluginErrorEvent> errors = [];
        _eventBus.Subscribe<PluginErrorEvent>((evt, _) =>
        {
            errors.Add(evt);
            return Task.CompletedTask;
        });

        await _manager.LoadPluginFromManifestAsync(manifestPath);

        errors.Should().ContainSingle();
        errors[0].PluginName.Should().Be("BadManifest");
        errors[0].ErrorMessage.Should().Contain("Invalid plugin manifest");
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_InvalidDll_PublishesErrorEvent()
    {
        Guid pluginId = Guid.NewGuid();
        string pluginDir = Path.Combine(_tempPluginsDir, "BadDll");
        Directory.CreateDirectory(pluginDir);

        string dllPath = Path.Combine(pluginDir, "BadDll.dll");
        await File.WriteAllTextAsync(dllPath, "not a valid dll");

        string manifestJson = $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""BadDll"",
            ""description"": ""A test"",
            ""version"": ""1.0.0"",
            ""assembly"": ""BadDll.dll""
        }}";
        string manifestPath = Path.Combine(pluginDir, "plugin.json");
        await File.WriteAllTextAsync(manifestPath, manifestJson);

        List<PluginErrorEvent> errors = [];
        _eventBus.Subscribe<PluginErrorEvent>((evt, _) =>
        {
            errors.Add(evt);
            return Task.CompletedTask;
        });

        await _manager.LoadPluginFromManifestAsync(manifestPath);

        errors.Should().ContainSingle();
        errors[0].PluginName.Should().Be("BadDll");
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_PrefersManifestOverDllScan()
    {
        Guid pluginId = Guid.NewGuid();
        string pluginDir = Path.Combine(_tempPluginsDir, "ManifestPlugin");
        Directory.CreateDirectory(pluginDir);

        string dllPath = Path.Combine(pluginDir, "ManifestPlugin.dll");
        await File.WriteAllTextAsync(dllPath, "garbage data");

        string manifestJson = $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""ManifestPlugin"",
            ""description"": ""Uses manifest"",
            ""version"": ""1.0.0"",
            ""assembly"": ""ManifestPlugin.dll""
        }}";
        string manifestPath = Path.Combine(pluginDir, "plugin.json");
        await File.WriteAllTextAsync(manifestPath, manifestJson);

        List<PluginErrorEvent> errors = [];
        _eventBus.Subscribe<PluginErrorEvent>((evt, _) =>
        {
            errors.Add(evt);
            return Task.CompletedTask;
        });

        await _manager.LoadPluginsFromDirectoryAsync();

        errors.Should().ContainSingle();
        errors[0].PluginName.Should().Be("ManifestPlugin");
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
