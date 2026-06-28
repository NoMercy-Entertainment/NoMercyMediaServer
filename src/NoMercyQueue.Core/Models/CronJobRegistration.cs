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
namespace NoMercyQueue.Core.Models;

/// <summary>
/// Declarative descriptor for a code-defined cron job. Emitted by
/// <c>AddCronJob&lt;T&gt;</c> at DI-registration time and consumed by
/// <c>CronWorker</c> on startup, so a single registration call is sufficient —
/// no separate runtime "schedule" call is required.
/// </summary>
/// <param name="ExecutorType">The <c>ICronJobExecutor</c> implementation type.</param>
/// <param name="JobType">Stable identifier used as the job's dedup/DB key.</param>
/// <param name="CronExpression">
/// Optional schedule override. When null, the executor's own
/// <c>CronExpression</c> is used.
/// </param>
public sealed record CronJobRegistration(
    Type ExecutorType,
    string JobType,
    string? CronExpression = null
);
