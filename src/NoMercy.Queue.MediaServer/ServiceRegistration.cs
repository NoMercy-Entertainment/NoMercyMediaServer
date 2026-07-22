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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Monitoring;
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
        services.AddSingleton<IQueueContext>(implementationFactory: _ => new EfQueueContextAdapter());
        services.AddSingleton<IConfigurationStore, MediaConfigurationStore>();

        // TryAdd so this shares the same instance as the encoder's own
        // default registration (see AddNoMercyEncoder) regardless of call order.
        services.TryAddSingleton<MediaActivityMonitor>();
        services.AddSingleton<IWorkerActivityGate, MediaPlaybackActivityGate>();
        services.AddSingleton(implementationFactory: sp =>
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
        services.AddSingleton<QueueRunner>(implementationFactory: sp =>
        {
            IQueueContext queueContext = sp.GetRequiredService<IQueueContext>();
            IConfigurationStore configStore = sp.GetRequiredService<IConfigurationStore>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            NmSystem.Lifecycle.IServerPhaseTracker? phaseTracker =
                sp.GetService<NmSystem.Lifecycle.IServerPhaseTracker>();
            IResourceBudget? resourceBudget = sp.GetService<IResourceBudget>();
            IWorkerActivityGate? activityGate = sp.GetService<IWorkerActivityGate>();
            RuntimeServerSettings rs = sp.GetRequiredService<RuntimeServerSettings>();
            QueueConfiguration configuration = new()
            {
                WorkerCounts = new()
                {
                    [key: rs.LibraryWorkers.Key] = rs.LibraryWorkers.Value,
                    [key: rs.ImportWorkers.Key] = rs.ImportWorkers.Value,
                    [key: rs.ExtrasWorkers.Key] = rs.ExtrasWorkers.Value,
                    [key: rs.EncoderWorkers.Key] = rs.EncoderWorkers.Value,
                    [key: rs.GpuEncoderWorkers.Key] = rs.GpuEncoderWorkers.Value,
                    [key: rs.CpuEncoderWorkers.Key] = rs.CpuEncoderWorkers.Value,
                    [key: rs.CronWorkers.Key] = rs.CronWorkers.Value,
                    [key: rs.ImageWorkers.Key] = rs.ImageWorkers.Value,
                    [key: rs.FileWorkers.Key] = rs.FileWorkers.Value,
                    [key: rs.MusicWorkers.Key] = rs.MusicWorkers.Value,
                    [key: rs.PaletteWorkers.Key] = rs.PaletteWorkers.Value,
                },
            };
            IReadOnlySet<string> resourceAwareQueues = new HashSet<string>
            {
                rs.GpuEncoderWorkers.Key,
                rs.CpuEncoderWorkers.Key,
            };

            // Encoder queues must not run until GPU/encoder detection
            // (BootStage.Hardware) completes. Detection is deferred to run after
            // the server is ready (BootStage.All), so these queues wait on both and
            // surface as paused in the dashboard until detection finishes.
            NmSystem.Lifecycle.BootStage encoderReady =
                NmSystem.Lifecycle.BootStage.All | NmSystem.Lifecycle.BootStage.Hardware;
            IReadOnlyDictionary<string, NmSystem.Lifecycle.BootStage> queueReadyStages =
                new Dictionary<string, NmSystem.Lifecycle.BootStage>
                {
                    [key: rs.EncoderWorkers.Key] = encoderReady,
                    [key: rs.GpuEncoderWorkers.Key] = encoderReady,
                    [key: rs.CpuEncoderWorkers.Key] = encoderReady,
                };

            return new(
                queueContext: queueContext,
                configuration: configuration,
                loggerFactory: loggerFactory,
                configurationStore: configStore,
                scopeFactory: scopeFactory,
                phaseTracker: phaseTracker,
                resourceBudget: resourceBudget,
                resourceAwareQueues: resourceAwareQueues,
                activityGate: activityGate,
                queueReadyStages: queueReadyStages
            );
        });
        services.AddSingleton<JobDispatcher>(implementationFactory: sp => sp.GetRequiredService<QueueRunner>().Dispatcher);

        // Phase 4.14 — orphan job recovery on boot. This hosted service's
        // StartAsync only runs once ASP.NET Core starts the host (RunHost /
        // RunWithHttpsRestart), which happens AFTER ServerBootstrapper calls
        // QueueRunner.Initialize() (and therefore after Initialize's
        // ResetAllReservedJobs() has already cleared every ReservedAt). Do not
        // assume this service sees reservations Initialize() hasn't touched
        // yet — ordering here is host-startup order, not registration order.
        services.AddHostedService<OrphanJobRecoveryHostedService>();

        // Stuck-reservation reaper — runs periodically for the lifetime of the
        // process so a job that hangs mid-flight (not just one interrupted by
        // a crash) doesn't hold its queue slot forever. Encoder queues are
        // excluded by design; see the class doc for why a wall-clock cutoff is
        // unsafe for them.
        services.AddHostedService<StuckReservationReaperHostedService>();

        return services;
    }
}
