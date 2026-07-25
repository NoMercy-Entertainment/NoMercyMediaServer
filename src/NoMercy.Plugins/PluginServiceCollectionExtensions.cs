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

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Pipeline;
using NoMercy.Events;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using NoMercy.Plugins.Hooks;
using NoMercy.Plugins.Verification;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Plugins;

public static class PluginServiceCollectionExtensions
{
    public static IServiceCollection AddPluginSystem(
        this IServiceCollection services,
        string pluginsPath
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsPath);

        services.AddSingleton<IPluginVerifier, PluginVerifier>();

        services.AddSingleton<IPluginConsentStore>(sp =>
        {
            IStorageDriver driver = sp.GetRequiredService<IStorageDriver>();
            IStorage storage = new LocalStorage(driver, new([pluginsPath], driver));
            string platformDataFolder = Path.Combine(pluginsPath, "data", "platform");
            IPluginConfiguration configuration = new PluginConfiguration(
                platformDataFolder,
                storage
            );
            return new ConfigPluginConsentStore(configuration);
        });

        services.AddSingleton<IPluginConsentService, PluginConsentService>();

        services.AddSingleton<IPluginManager>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            ILogger<PluginManager> logger = sp.GetRequiredService<ILogger<PluginManager>>();
            IStorageDriver driver = sp.GetRequiredService<IStorageDriver>();
            IPluginVerifier verifier = sp.GetRequiredService<IPluginVerifier>();
            IPluginConsentService consentService = sp.GetRequiredService<IPluginConsentService>();
            IStorage storage = new LocalStorage(driver, new([pluginsPath], driver));
            return new PluginManager(
                eventBus,
                sp,
                logger,
                pluginsPath,
                storage,
                driver,
                verifier,
                consentService
            );
        });

        // Wire encoder plugins' GetProfile into the encoder's profile-override seam.
        // First plugin returning a non-null profile for the source wins.
        services.AddSingleton<IProfileOverride, PluginProfileOverride>();

        services.AddSingleton<IPluginCronRegistrar, PluginCronRegistrar>();

        // Additive auth claims: OnTokenValidated (ServiceConfiguration.Auth.cs) resolves
        // this per authenticated request to enrich the principal. It never decides auth.
        services.AddSingleton<IPluginClaimsAugmentor, PluginClaimsAugmentor>();

        return services;
    }

    public static void RegisterPluginServices(
        this IServiceCollection services,
        PluginManager pluginManager
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pluginManager);

        foreach (IPluginServiceRegistrator registrator in pluginManager.GetServiceRegistrators())
        {
            registrator.RegisterServices(services);
        }
    }

    // Pre-build registration: discovers IPluginServiceRegistrator implementations before
    // the DI container is built so plugins can contribute services to the host container.
    //
    // LIMITATION: This loads each plugin assembly into a temporary AssemblyLoadContext for
    // service-registration discovery only. The runtime load (LoadAllAsync) loads it again
    // into the canonical context. This two-phase approach is a known-fragile MVI shortcut —
    // see the future refactor TODO: introduce a proper plugin DI sub-container that avoids
    // loading the same assembly twice in different contexts.
    public static IServiceCollection RegisterPluginServicesFromManifests(
        this IServiceCollection services,
        string pluginsPath
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!Directory.Exists(pluginsPath))
            return services;

        foreach (string pluginDir in Directory.EnumerateDirectories(pluginsPath))
        {
            string dirName = Path.GetFileName(pluginDir);
            if (dirName is "configurations" or "data")
                continue;

            string manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                string manifestJson = File.ReadAllText(manifestPath);
                PluginManifest manifest = PluginManifestParser.Parse(manifestJson);
                string assemblyPath = Path.Combine(pluginDir, manifest.Assembly);

                if (!File.Exists(assemblyPath))
                    continue;

                // Load into a temporary context for discovery only; unloaded after registration.
                PluginLoadContext discoveryCtx = new(assemblyPath);
                try
                {
                    Assembly assembly = discoveryCtx.LoadFromAssemblyPath(assemblyPath);

                    IEnumerable<Type> registratorTypes = assembly
                        .GetTypes()
                        .Where(t =>
                            typeof(IPluginServiceRegistrator).IsAssignableFrom(t)
                            && t is { IsAbstract: false, IsInterface: false }
                        );

                    foreach (Type registratorType in registratorTypes)
                    {
                        if (
                            Activator.CreateInstance(registratorType)
                            is IPluginServiceRegistrator registrator
                        )
                            registrator.RegisterServices(services);
                    }
                }
                finally
                {
                    discoveryCtx.Unload();
                }
            }
            catch (Exception)
            {
                // Never throw during ConfigureServices — boot must continue without this plugin's services.
            }
        }

        return services;
    }
}
