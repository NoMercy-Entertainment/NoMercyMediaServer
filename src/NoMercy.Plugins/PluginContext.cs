using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

public class PluginContext : IPluginContext
{
    public IEventBus EventBus { get; }
    public IServiceProvider Services { get; }
    public ILogger Logger { get; }
    public string DataFolderPath { get; }

    public PluginContext(IEventBus eventBus, IServiceProvider services, ILogger logger, string dataFolderPath)
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DataFolderPath = dataFolderPath ?? throw new ArgumentNullException(nameof(dataFolderPath));
    }
}
