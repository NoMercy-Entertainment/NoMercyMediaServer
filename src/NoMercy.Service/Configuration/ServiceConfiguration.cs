using NoMercy.NmSystem.Information;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        ConfigureKestrel(services);
        ConfigureHttpClients(services);
        ConfigureCoreServices(services);
        ConfigureLogging(services);
        ConfigureAuth(services);
        ConfigureApi(services);
        ConfigureCors(services);
        ConfigureCronJobs(services);
    }

    private static void ConfigureKestrel(IServiceCollection services) { }

    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            // Logging filters are handled by CustomLogger's message filtering
            // since it replaces ILogger<T> and bypasses the built-in filter pipeline
        });
    }

    private static void ConfigureCors(IServiceCollection services)
    {
        // Configure CORS
        services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowNoMercyOrigins",
                builder =>
                {
                    List<string> origins =
                    [
                        "https://nomercy.tv",
                        "https://*.nomercy.tv",
                        "https://cast.nomercy.tv",
                        "https://hlsjs.video-dev.org",
                        "http://localhost:7625",
                    ];

                    if (Config.IsDev)
                    {
                        origins.Add("http://192.168.2.201:5501");
                        origins.Add("http://192.168.2.201:5502");
                        origins.Add("http://192.168.2.201:5503");
                        origins.Add("http://localhost");
                        origins.Add("https://localhost");
                    }

                    builder
                        .WithOrigins(origins.ToArray())
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .WithHeaders("Access-Control-Allow-Private-Network", "true")
                        .WithHeaders("Access-Control-Allow-Headers", "*")
                        .AllowAnyHeader();
                }
            );
        });
    }

    private static Ulid? TryGetDeviceId(HttpContext httpContext)
    {
        string? raw = httpContext.Request.Query["client_id"].FirstOrDefault();
        if (string.IsNullOrEmpty(raw))
            return null;

        return Ulid.TryParse(raw, out Ulid id) ? id : null;
    }
}
