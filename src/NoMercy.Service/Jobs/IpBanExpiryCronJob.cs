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

using NoMercy.Api.Security;
using NoMercy.Data.Security;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Service.Jobs;

// Bans already expire without this: IsBannedAsync compares against the clock.
// The sweep exists to stop the table growing forever and to reconcile the
// in-memory cache with whatever the database actually holds.
public class IpBanExpiryCronJob(
    IIpBanRepository repository,
    IAbuseGuard abuseGuard,
    ILogger<IpBanExpiryCronJob> logger
) : ICronJobExecutor
{
    private const int RetentionDays = 30;

    public string CronExpression => new CronExpressionBuilder().Hourly();

    public string JobName => "IP Ban Expiry";

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        int purged = await repository.PurgeExpiredAsync(cutoff, cancellationToken);
        await abuseGuard.RefreshAsync(cancellationToken);

        if (purged > 0)
            logger.LogInformation("Removed {Count} expired ip bans", purged);
    }
}
