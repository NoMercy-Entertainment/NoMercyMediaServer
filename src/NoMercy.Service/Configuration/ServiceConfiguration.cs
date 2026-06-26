// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.NmSystem.Information;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register IServerConfiguration FIRST: ConfigureHttpClients eagerly builds a
        // provider and resolves it, so it must exist before any sub-config runs. The
        // production Program.cs path registers it early too; this keeps the Startup
        // path (used by the API test host) consistent.
        services.Configure<ServerConfiguration>(configuration.GetSection("Server"));
        services.AddSingleton<IServerConfiguration, ServerConfigurationWrapper>();

        ConfigureKestrel(services);
        ConfigureHttpClients(services);
        ConfigureCoreServices(services, configuration);
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
