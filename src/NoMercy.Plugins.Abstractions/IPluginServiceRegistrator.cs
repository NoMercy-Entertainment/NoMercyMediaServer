using Microsoft.Extensions.DependencyInjection;

namespace NoMercy.Plugins.Abstractions;

public interface IPluginServiceRegistrator
{
    void RegisterServices(IServiceCollection services);
}
