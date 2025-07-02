using Asp.Versioning.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Server.AppConfig;

namespace NoMercy.Server;

public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly StartupOptions _options;

    public Startup(IConfiguration configuration, IWebHostEnvironment environment,
        IApiVersionDescriptionProvider provider, StartupOptions options)
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
        using IServiceScope? serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()?.CreateScope();
        MediaContext? mediaContext = serviceScope?.ServiceProvider.GetRequiredService<MediaContext>();
        QueueContext? queueContext = serviceScope?.ServiceProvider.GetRequiredService<QueueContext>();

        try
        {
            // Check if migration is needed
            if (mediaContext?.Database.GetPendingMigrations().Any() == true)
            {
                mediaContext.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
                mediaContext.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");
                mediaContext.Database.Migrate();
                Console.WriteLine("Media database migrations applied.");
            }

            if (queueContext?.Database.GetPendingMigrations().Any() == true)
            {
                queueContext.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
                queueContext.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");
                queueContext.Database.Migrate();
                Console.WriteLine("Queue database migrations applied.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying migrations: {ex.Message}");
        }

        ApplicationConfiguration.ConfigureApp(app, _provider);
    }
}