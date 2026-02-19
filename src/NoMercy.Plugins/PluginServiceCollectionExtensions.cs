using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

public static class PluginServiceCollectionExtensions
{
    public static IServiceCollection AddPluginSystem(this IServiceCollection services, string pluginsPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsPath);

        services.AddSingleton<IPluginManager>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            ILogger<PluginManager> logger = sp.GetRequiredService<ILogger<PluginManager>>();
            return new PluginManager(eventBus, sp, logger, pluginsPath);
        });

        return services;
    }

    public static void RegisterPluginServices(this IServiceCollection services, PluginManager pluginManager)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pluginManager);

        foreach (IPluginServiceRegistrator registrator in pluginManager.GetServiceRegistrators())
        {
            registrator.RegisterServices(services);
        }
    }
}
