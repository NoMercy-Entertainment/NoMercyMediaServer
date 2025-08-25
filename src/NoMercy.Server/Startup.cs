using Asp.Versioning.ApiExplorer;
using NoMercy.Server.AppConfig;

namespace NoMercy.Server;

public class Startup
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly StartupOptions _options;

    public Startup(IApiVersionDescriptionProvider provider, StartupOptions options)
    {
        _provider = provider;
        _options = options;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ServiceConfiguration.ConfigureServices(services);

        // Add the StartupOptions and SeedingOptions to the service container
        services.AddSingleton(_options);
    }

    public void Configure(IApplicationBuilder app)
    {
        ApplicationConfiguration.ConfigureApp(app, _provider);
    }
}