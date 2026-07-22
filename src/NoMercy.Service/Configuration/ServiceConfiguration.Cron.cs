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

using NoMercy.MediaProcessing.Jobs.ChangesJobs;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercy.Service.Jobs;
using NoMercyQueue.Extensions;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    private static void ConfigureCronJobs(IServiceCollection services)
    {
        services.AddCronJob<CertificateRenewalCronJob>(jobType: "certificate-renewal");
        services.AddCronJob<ActivityLogRetentionCronJob>(jobType: "activity-log-retention");
        services.AddCronJob<TmdbChangesCronJob>(jobType: "tmdb-changes-sync");
        services.AddCronJob<DeviceDropRuleCronJob>(jobType: "device-drop-rule-job");
        services.AddCronJob<ServerUserSyncCronJob>(jobType: "server-user-sync");
        services.AddCronJob<DatabaseBackupCronJob>(jobType: "database-backup");
    }
}
