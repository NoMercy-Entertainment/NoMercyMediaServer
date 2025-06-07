using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Server.AppConfig;

namespace NoMercy.Server;

public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly StartupOptions _options;

    public Startup(IConfiguration configuration, IWebHostEnvironment environment, IApiVersionDescriptionProvider provider, StartupOptions options)
    {
        _configuration = configuration;
        _environment = environment;
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