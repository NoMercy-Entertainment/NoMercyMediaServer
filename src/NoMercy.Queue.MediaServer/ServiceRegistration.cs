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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.Queue.MediaServer.Configuration;
using NoMercy.Resources;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Core.Resources;

namespace NoMercy.Queue.MediaServer;

public static class ServiceRegistration
{
    public static IServiceCollection AddMediaServerQueue(this IServiceCollection services)
    {
        services.AddSingleton<IQueueContext>(_ => new EfQueueContextAdapter());
        services.AddSingleton<IConfigurationStore, MediaConfigurationStore>();
        services.AddSingleton(sp =>
        {
            EncoderResourceConfig resources = sp.GetRequiredService<
                IOptions<EncoderResourceConfig>
            >().Value;

            return new ResourceBudgetOptions(
                CpuHeadroomPercent: resources.EncoderCpuHeadroomPercent,
                GpuHeadroomPercent: resources.EncoderGpuHeadroomPercent,
                MinFreeMemoryMb: resources.EncoderMinFreeMemoryMb
            );
        });
        services.AddSingleton<QueueRunner>(sp =>
        {
            IQueueContext queueContext = sp.GetRequiredService<IQueueContext>();
            IConfigurationStore configStore = sp.GetRequiredService<IConfigurationStore>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            NmSystem.Lifecycle.IServerPhaseTracker? phaseTracker =
                sp.GetService<NmSystem.Lifecycle.IServerPhaseTracker>();
            IResourceBudget? resourceBudget = sp.GetService<IResourceBudget>();
            RuntimeServerSettings rs = sp.GetRequiredService<RuntimeServerSettings>();
            QueueConfiguration configuration = new()
            {
                WorkerCounts = new()
                {
                    [rs.LibraryWorkers.Key] = rs.LibraryWorkers.Value,
                    [rs.ImportWorkers.Key] = rs.ImportWorkers.Value,
                    [rs.ExtrasWorkers.Key] = rs.ExtrasWorkers.Value,
                    [rs.EncoderWorkers.Key] = rs.EncoderWorkers.Value,
                    [rs.GpuEncoderWorkers.Key] = rs.GpuEncoderWorkers.Value,
                    [rs.CpuEncoderWorkers.Key] = rs.CpuEncoderWorkers.Value,
                    [rs.CronWorkers.Key] = rs.CronWorkers.Value,
                    [rs.ImageWorkers.Key] = rs.ImageWorkers.Value,
                    [rs.FileWorkers.Key] = rs.FileWorkers.Value,
                    [rs.MusicWorkers.Key] = rs.MusicWorkers.Value,
                    [rs.PaletteWorkers.Key] = rs.PaletteWorkers.Value,
                },
            };
            IReadOnlySet<string> resourceAwareQueues = new HashSet<string>
            {
                rs.GpuEncoderWorkers.Key,
                rs.CpuEncoderWorkers.Key,
            };

            return new(
                queueContext,
                configuration,
                loggerFactory,
                configStore,
                scopeFactory,
                phaseTracker,
                resourceBudget,
                resourceAwareQueues
            );
        });
        services.AddSingleton<JobDispatcher>(sp => sp.GetRequiredService<QueueRunner>().Dispatcher);

        // Phase 4.14 — orphan job recovery on boot. Runs before QueueRunner
        // resets reserved jobs, so we can distinguish first-time orphans
        // (which deserve one retry) from repeat offenders (which get
        // moved to FailedJobs).
        services.AddHostedService<OrphanJobRecoveryHostedService>();

        return services;
    }
}
