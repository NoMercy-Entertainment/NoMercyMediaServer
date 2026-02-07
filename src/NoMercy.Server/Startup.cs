using Asp.Versioning.ApiExplorer;
using NoMercy.Server.AppConfig;

namespace NoMercy.Server;

public class Startup
{
    private readonly StartupOptions _options;

    public Startup(StartupOptions options)
    {
        _options = options;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ServiceConfiguration.ConfigureServices(services);

        services.AddSingleton(_options);
    }

    public void Configure(IApplicationBuilder app)
    {
        IApiVersionDescriptionProvider provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
        ApplicationConfiguration.ConfigureApp(app, provider);
    }
}