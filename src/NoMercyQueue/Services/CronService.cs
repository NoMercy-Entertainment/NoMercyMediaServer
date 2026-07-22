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

using NCrontab;

namespace NoMercyQueue.Services;

public class CronService
{
    public static DateTime GetNextOccurrence(string cronExpression, DateTime baseTime)
    {
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);
        return schedule.GetNextOccurrence(baseTime: baseTime);
    }

    public static bool ShouldRun(string cronExpression, DateTime lastRun, DateTime currentTime)
    {
        DateTime nextRun = GetNextOccurrence(cronExpression: cronExpression, baseTime: lastRun);
        return currentTime >= nextRun;
    }
}
