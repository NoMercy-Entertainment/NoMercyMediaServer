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
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;

namespace NoMercyQueue.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCronWorker(this IServiceCollection services)
    {
        services.AddSingleton<CronWorker>();
        services.AddHostedService<CronWorker>(implementationFactory: provider =>
            provider.GetRequiredService<CronWorker>()
        );

        return services;
    }

    public static IServiceCollection RegisterCronJob<T>(
        this IServiceCollection services,
        string jobType
    )
        where T : class, ICronJobExecutor
    {
        services.AddScoped<T>();
        return services;
    }

    public static IServiceCollection AddCronJob<T>(
        this IServiceCollection services,
        string jobType,
        string? cronExpression = null
    )
        where T : class, ICronJobExecutor
    {
        services.AddScoped<T>();
        services.AddSingleton(implementationInstance: new CronJobRegistration(ExecutorType: typeof(T), JobType: jobType, CronExpression: cronExpression));
        return services;
    }

    public static void RegisterJobWithSchedule<T>(
        this CronWorker cronWorker,
        string jobType,
        IServiceProvider serviceProvider
    )
        where T : class, ICronJobExecutor
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        T tempInstance = scope.ServiceProvider.GetRequiredService<T>();

        cronWorker.RegisterJob<T>(jobType: jobType, name: tempInstance.JobName, cronExpression: tempInstance.CronExpression);
    }
}
