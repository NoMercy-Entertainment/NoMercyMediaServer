using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

public class PluginManager : IPluginManager, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginsPath;
    private readonly ConcurrentDictionary<Guid, LoadedPlugin> _loadedPlugins = new();

    public PluginManager(IEventBus eventBus, IServiceProvider serviceProvider, ILogger<PluginManager> logger, string pluginsPath)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginsPath = pluginsPath ?? throw new ArgumentNullException(nameof(pluginsPath));
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        return _loadedPlugins.Values
            .Select(lp => lp.Info)
            .ToList()
            .AsReadOnly();
    }

    public async Task InstallPluginAsync(string packagePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        string fullPath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {fullPath}", fullPath);
        }

        string pluginName = Path.GetFileNameWithoutExtension(fullPath);
        string pluginDir = Path.Combine(_pluginsPath, pluginName);

        if (!Directory.Exists(pluginDir))
        {
            Directory.CreateDirectory(pluginDir);
        }

        string destPath = Path.Combine(pluginDir, Path.GetFileName(fullPath));
        File.Copy(fullPath, destPath, overwrite: true);

        await LoadPluginAssemblyAsync(destPath, ct);
    }

    public async Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException($"Plugin {pluginId} is not installed.");
        }

        if (loaded.Info.Status == PluginStatus.Active)
        {
            return;
        }

        if (loaded.Instance is null && loaded.Info.AssemblyPath is not null)
        {
            await LoadPluginAssemblyAsync(loaded.Info.AssemblyPath, ct);
            return;
        }

        if (loaded.Instance is not null)
        {
            try
            {
                string dataFolder = Path.Combine(_pluginsPath, "data", pluginId.ToString("N"));
                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }

                PluginContext context = new(_eventBus, _serviceProvider, _logger, dataFolder);
                loaded.Instance.Initialize(context);
                PluginLifecycle.Transition(loaded.Info, PluginStatus.Active);

                await _eventBus.PublishAsync(new PluginLoadedEvent
                {
                    PluginId = pluginId.ToString(),
                    PluginName = loaded.Info.Name,
                    Version = loaded.Info.Version.ToString()
                }, ct);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                PluginLifecycle.Transition(loaded.Info, PluginStatus.Malfunctioned);

                await _eventBus.PublishAsync(new PluginErrorEvent
                {
                    PluginId = pluginId.ToString(),
                    PluginName = loaded.Info.Name,
                    ErrorMessage = ex.Message,
                    ExceptionType = ex.GetType().Name
                }, ct);
            }
        }
    }

    public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException($"Plugin {pluginId} is not installed.");
        }

        if (loaded.Info.Status == PluginStatus.Disabled)
        {
            return Task.CompletedTask;
        }

        loaded.Instance?.Dispose();
        PluginLifecycle.Transition(loaded.Info, PluginStatus.Disabled);

        return Task.CompletedTask;
    }

    public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_loadedPlugins.TryRemove(pluginId, out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException($"Plugin {pluginId} is not installed.");
        }

        loaded.Instance?.Dispose();
        loaded.LoadContext?.Unload();
        PluginLifecycle.Transition(loaded.Info, PluginStatus.Deleted);

        if (loaded.Info.AssemblyPath is not null)
        {
            string? pluginDir = Path.GetDirectoryName(loaded.Info.AssemblyPath);
            if (pluginDir is not null && Directory.Exists(pluginDir))
            {
                try
                {
                    Directory.Delete(pluginDir, recursive: true);
                }
                catch (IOException)
                {
                    _logger.LogWarning("Could not delete plugin directory {PluginDir}. Files may be locked.", pluginDir);
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task LoadPluginsFromDirectoryAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_pluginsPath))
        {
            return;
        }

        foreach (string pluginDir in Directory.GetDirectories(_pluginsPath))
        {
            string dirName = Path.GetFileName(pluginDir);
            if (dirName is "configurations" or "data")
            {
                continue;
            }

            string manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (File.Exists(manifestPath))
            {
                await LoadPluginFromManifestAsync(manifestPath, ct);
                continue;
            }

            string[] dllFiles = Directory.GetFiles(pluginDir, "*.dll");
            foreach (string dllPath in dllFiles)
            {
                await LoadPluginAssemblyAsync(dllPath, ct);
            }
        }
    }

    internal async Task LoadPluginFromManifestAsync(string manifestPath, CancellationToken ct = default)
    {
        string pluginDir = Path.GetDirectoryName(manifestPath)!;

        try
        {
            PluginManifest manifest = await PluginManifestParser.ParseFileAsync(manifestPath, ct);
            string assemblyPath = Path.Combine(pluginDir, manifest.Assembly);

            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning(
                    "Plugin manifest {ManifestPath} references assembly '{Assembly}' which was not found.",
                    manifestPath, manifest.Assembly);

                await _eventBus.PublishAsync(new PluginErrorEvent
                {
                    PluginId = manifest.Id.ToString(),
                    PluginName = manifest.Name,
                    ErrorMessage = $"Assembly '{manifest.Assembly}' not found in plugin directory.",
                    ExceptionType = nameof(FileNotFoundException)
                }, ct);

                return;
            }

            PluginLoadContext loadContext = new(assemblyPath);

            try
            {
                Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                List<Type> pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                    .ToList();

                bool foundPlugin = false;

                foreach (Type pluginType in pluginTypes)
                {
                    IPlugin? instance = Activator.CreateInstance(pluginType) as IPlugin;
                    if (instance is null)
                    {
                        continue;
                    }

                    PluginStatus initialStatus = manifest.AutoEnabled ? PluginStatus.Active : PluginStatus.Disabled;

                    if (manifest.AutoEnabled)
                    {
                        string dataFolder = Path.Combine(_pluginsPath, "data", instance.Id.ToString("N"));
                        if (!Directory.Exists(dataFolder))
                        {
                            Directory.CreateDirectory(dataFolder);
                        }

                        PluginContext context = new(_eventBus, _serviceProvider, _logger, dataFolder);

                        try
                        {
                            instance.Initialize(context);
                        }
                        catch (Exception ex)
                        {
                            initialStatus = PluginStatus.Malfunctioned;
                            instance.Dispose();

                            PluginInfo errorInfo = PluginManifestParser.ToPluginInfo(manifest, assemblyPath, initialStatus, manifestPath);

                            LoadedPlugin errorLoaded = new(errorInfo, null, loadContext);
                            _loadedPlugins[manifest.Id] = errorLoaded;
                            foundPlugin = true;

                            await _eventBus.PublishAsync(new PluginErrorEvent
                            {
                                PluginId = manifest.Id.ToString(),
                                PluginName = manifest.Name,
                                ErrorMessage = ex.Message,
                                ExceptionType = ex.GetType().Name
                            }, ct);

                            continue;
                        }
                    }

                    PluginInfo info = PluginManifestParser.ToPluginInfo(manifest, assemblyPath, initialStatus, manifestPath);

                    IPlugin? storedInstance = initialStatus == PluginStatus.Active ? instance : null;
                    LoadedPlugin loaded = new(info, storedInstance, loadContext);
                    _loadedPlugins[manifest.Id] = loaded;
                    foundPlugin = true;

                    if (initialStatus == PluginStatus.Active)
                    {
                        await _eventBus.PublishAsync(new PluginLoadedEvent
                        {
                            PluginId = manifest.Id.ToString(),
                            PluginName = manifest.Name,
                            Version = manifest.Version
                        }, ct);
                    }
                }

                if (!foundPlugin)
                {
                    loadContext.Unload();
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                string errorMessage = string.Join("; ", ex.LoaderExceptions
                    .Where(e => e is not null)
                    .Select(e => e!.Message));

                _logger.LogWarning("Failed to load plugin assembly {AssemblyPath}: {Error}", assemblyPath, errorMessage);

                await _eventBus.PublishAsync(new PluginErrorEvent
                {
                    PluginId = manifest.Id.ToString(),
                    PluginName = manifest.Name,
                    ErrorMessage = errorMessage,
                    ExceptionType = nameof(ReflectionTypeLoadException)
                }, ct);

                loadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load plugin assembly {AssemblyPath}: {Error}", assemblyPath, ex.Message);

                await _eventBus.PublishAsync(new PluginErrorEvent
                {
                    PluginId = manifest.Id.ToString(),
                    PluginName = manifest.Name,
                    ErrorMessage = ex.Message,
                    ExceptionType = ex.GetType().Name
                }, ct);

                loadContext.Unload();
            }
        }
        catch (Exception ex)
        {
            string pluginName = Path.GetFileName(pluginDir);

            _logger.LogWarning("Failed to parse plugin manifest {ManifestPath}: {Error}", manifestPath, ex.Message);

            await _eventBus.PublishAsync(new PluginErrorEvent
            {
                PluginId = Guid.Empty.ToString(),
                PluginName = pluginName,
                ErrorMessage = $"Invalid plugin manifest: {ex.Message}",
                ExceptionType = ex.GetType().Name
            }, ct);
        }
    }

    internal async Task LoadPluginAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        PluginLoadContext loadContext = new(assemblyPath);

        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            List<Type> pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                .ToList();

            foreach (Type pluginType in pluginTypes)
            {
                IPlugin? instance = Activator.CreateInstance(pluginType) as IPlugin;
                if (instance is null)
                {
                    continue;
                }

                string dataFolder = Path.Combine(_pluginsPath, "data", instance.Id.ToString("N"));
                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }

                PluginContext context = new(_eventBus, _serviceProvider, _logger, dataFolder);

                try
                {
                    instance.Initialize(context);

                    PluginInfo info = new()
                    {
                        Id = instance.Id,
                        Name = instance.Name,
                        Description = instance.Description,
                        Version = instance.Version,
                        Status = PluginStatus.Active,
                        AssemblyPath = assemblyPath
                    };

                    LoadedPlugin loaded = new(info, instance, loadContext);
                    _loadedPlugins[instance.Id] = loaded;

                    await _eventBus.PublishAsync(new PluginLoadedEvent
                    {
                        PluginId = instance.Id.ToString(),
                        PluginName = instance.Name,
                        Version = instance.Version.ToString()
                    }, ct);
                }
                catch (Exception ex)
                {
                    instance.Dispose();

                    PluginInfo info = new()
                    {
                        Id = instance.Id,
                        Name = instance.Name,
                        Description = instance.Description,
                        Version = instance.Version,
                        Status = PluginStatus.Malfunctioned,
                        AssemblyPath = assemblyPath
                    };

                    LoadedPlugin loaded = new(info, null, loadContext);
                    _loadedPlugins[instance.Id] = loaded;

                    await _eventBus.PublishAsync(new PluginErrorEvent
                    {
                        PluginId = instance.Id.ToString(),
                        PluginName = instance.Name,
                        ErrorMessage = ex.Message,
                        ExceptionType = ex.GetType().Name
                    }, ct);
                }
            }

            if (pluginTypes.Count == 0)
            {
                loadContext.Unload();
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            string errorMessage = string.Join("; ", ex.LoaderExceptions
                .Where(e => e is not null)
                .Select(e => e!.Message));

            _logger.LogWarning("Failed to load plugin assembly {AssemblyPath}: {Error}", assemblyPath, errorMessage);

            await _eventBus.PublishAsync(new PluginErrorEvent
            {
                PluginId = Guid.Empty.ToString(),
                PluginName = assemblyName,
                ErrorMessage = errorMessage,
                ExceptionType = nameof(ReflectionTypeLoadException)
            }, ct);

            loadContext.Unload();
        }
        catch (Exception ex)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            _logger.LogWarning("Failed to load plugin assembly {AssemblyPath}: {Error}", assemblyPath, ex.Message);

            await _eventBus.PublishAsync(new PluginErrorEvent
            {
                PluginId = Guid.Empty.ToString(),
                PluginName = assemblyName,
                ErrorMessage = ex.Message,
                ExceptionType = ex.GetType().Name
            }, ct);

            loadContext.Unload();
        }
    }

    public IPlugin? GetPluginInstance(Guid pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            return loaded.Instance;
        }

        return null;
    }

    public IEnumerable<T> GetPluginsOfType<T>() where T : IPlugin
    {
        return _loadedPlugins.Values
            .Where(lp => lp.Instance is T && lp.Info.Status == PluginStatus.Active)
            .Select(lp => (T)lp.Instance!)
            .ToList();
    }

    public void Dispose()
    {
        foreach (LoadedPlugin loaded in _loadedPlugins.Values)
        {
            loaded.Instance?.Dispose();
            loaded.LoadContext?.Unload();
        }

        _loadedPlugins.Clear();
    }

    internal sealed class LoadedPlugin(PluginInfo info, IPlugin? instance, PluginLoadContext? loadContext)
    {
        public PluginInfo Info { get; } = info;
        public IPlugin? Instance { get; } = instance;
        public PluginLoadContext? LoadContext { get; } = loadContext;
    }
}
