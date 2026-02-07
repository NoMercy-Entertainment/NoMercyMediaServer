## 11. Plugin System Architecture

### 11.1 Design (Inspired by Jellyfin)

Based on analysis of Jellyfin's proven plugin architecture, adapted for NoMercy:

```
plugins/
├── MyPlugin/
│   ├── plugin.json              # Plugin manifest
│   ├── MyPlugin.dll             # Compiled .NET assembly
│   └── config.json              # Plugin configuration (JSON, not XML)
```

### 11.2 Core Contracts (`NoMercy.Plugins.Abstractions`)

```csharp
public interface IPlugin : IDisposable
{
    string Name { get; }
    string Description { get; }
    Guid Id { get; }
    Version Version { get; }
    void Initialize(IPluginContext context);
}

public interface IPluginContext
{
    IEventBus EventBus { get; }
    IServiceProvider Services { get; }
    ILogger Logger { get; }
    string DataFolderPath { get; }
}

// Extension point interfaces
public interface IMetadataPlugin : IPlugin
{
    Task<MediaMetadata?> GetMetadataAsync(string title, MediaType type);
}

public interface IMediaSourcePlugin : IPlugin
{
    Task<IEnumerable<MediaFile>> ScanAsync(string path);
}

public interface IEncoderPlugin : IPlugin
{
    EncodingProfile GetProfile(MediaInfo info);
}

public interface IAuthPlugin : IPlugin
{
    Task<AuthResult> AuthenticateAsync(string token);
}

public interface IScheduledTaskPlugin : IPlugin
{
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken ct);
}

public interface IPluginServiceRegistrator
{
    void RegisterServices(IServiceCollection services);
}
```

### 11.3 Plugin Manager

```csharp
public interface IPluginManager
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    Task InstallPluginAsync(string packageUrl);
    Task EnablePluginAsync(Guid pluginId);
    Task DisablePluginAsync(Guid pluginId);
    Task UninstallPluginAsync(Guid pluginId);
}
```

Key features:
- Each plugin loads in isolated `AssemblyLoadContext`
- Plugin manifest (`plugin.json`) with GUID, version, `targetAbi`
- Plugin configuration stored as JSON (not XML like Jellyfin)
- Lifecycle: Active → Disabled → Malfunctioned → Deleted
- Repository system for remote plugin discovery/installation
- Hot-reload capability via AssemblyLoadContext unloading

### 11.4 Plugin Event Integration

Plugins subscribe to domain events via the `IEventBus`:
```csharp
public class ScrobblerPlugin : IPlugin
{
    public void Initialize(IPluginContext context)
    {
        context.EventBus.Subscribe<PlaybackCompletedEvent>(async (evt, ct) =>
        {
            await ScrobbleToLastFmAsync(evt.MediaTitle, evt.Duration);
        });
    }
}
```

### 11.5 Implementation Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| PLG-01 | Create `NoMercy.Plugins.Abstractions` project with all interfaces | Medium |
| PLG-02 | Implement `PluginManager` with AssemblyLoadContext loading | Large |
| PLG-03 | Implement plugin manifest parsing (`plugin.json`) | Small |
| PLG-04 | Implement plugin lifecycle state machine | Medium |
| PLG-05 | Implement `IPluginServiceRegistrator` DI integration | Medium |
| PLG-06 | Create plugin configuration system (JSON-based) | Medium |
| PLG-07 | Create plugin repository/manifest system | Large |
| PLG-08 | Add plugin management API endpoints | Medium |
| PLG-09 | Create plugin template project/NuGet package | Medium |
| PLG-10 | Implement plugin auto-update mechanism | Large |
| PLG-11 | Add plugin sandboxing (permission model) | Large |

---

