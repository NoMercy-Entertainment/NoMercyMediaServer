using Microsoft.Extensions.Logging;
using NoMercy.Events;

namespace NoMercy.Plugins.Abstractions;

public interface IPluginContext
{
    IEventBus EventBus { get; }
    IServiceProvider Services { get; }
    ILogger Logger { get; }
    string DataFolderPath { get; }
    IPluginConfiguration Configuration { get; }
}
