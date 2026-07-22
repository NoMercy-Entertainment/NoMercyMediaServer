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
        services.Configure<ServerConfiguration>(config: configuration.GetSection(key: "Server"));
        services.AddSingleton<IServerConfiguration, ServerConfigurationWrapper>();

        ConfigureKestrel(services: services);
        ConfigureHttpClients(services: services);
        ConfigureCoreServices(services: services, configuration: configuration);
        ConfigureLogging(services: services);
        ConfigureAuth(services: services);
        ConfigureApi(services: services);
        ConfigureCors(services: services);
        ConfigureCronJobs(services: services);
    }

    private static void ConfigureKestrel(IServiceCollection services) { }

    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(configure: logging =>
        {
            // Logging filters are handled by CustomLogger's message filtering
            // since it replaces ILogger<T> and bypasses the built-in filter pipeline
        });
    }

    private static void ConfigureCors(IServiceCollection services)
    {
        // Configure CORS
        services.AddCors(setupAction: options =>
        {
            options.AddPolicy(
                name: "AllowNoMercyOrigins",
                configurePolicy: builder =>
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
                        origins.Add(item: "http://192.168.2.201:5501");
                        origins.Add(item: "http://192.168.2.201:5502");
                        origins.Add(item: "http://192.168.2.201:5503");
                        origins.Add(item: "http://localhost");
                        origins.Add(item: "https://localhost");
                    }

                    builder
                        .WithOrigins(origins: origins.ToArray())
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader();
                }
            );
        });
    }

    private static Ulid? TryGetDeviceId(HttpContext httpContext)
    {
        string? raw = httpContext.Request.Query[key: "client_id"].FirstOrDefault();
        if (string.IsNullOrEmpty(value: raw))
            return null;

        return Ulid.TryParse(base32: raw, ulid: out Ulid id) ? id : null;
    }
}
