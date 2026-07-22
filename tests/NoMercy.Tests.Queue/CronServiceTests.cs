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

using FluentAssertions;
using NoMercyQueue.Services;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="CronService.ShouldRun"/> is the public decision function a
/// caller uses to ask "has this cron job's next occurrence, computed from its
/// last run, already arrived?" — a plugin-facing alternative to CronWorker's
/// own internal NextRun bookkeeping loop.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class CronServiceTests
{
    [Fact]
    public void ShouldRun_CurrentTimeAtOrAfterNextOccurrence_ReturnsTrue()
    {
        DateTime lastRun = new(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);
        DateTime currentTime = new(year: 2025, month: 1, day: 2, hour: 0, minute: 0, second: 1, kind: DateTimeKind.Utc);

        CronService.ShouldRun(cronExpression: "0 0 * * *", lastRun: lastRun, currentTime: currentTime).Should().BeTrue();
    }

    [Fact]
    public void ShouldRun_CurrentTimeBeforeNextOccurrence_ReturnsFalse()
    {
        DateTime lastRun = new(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);
        DateTime currentTime = new(year: 2025, month: 1, day: 1, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);

        CronService.ShouldRun(cronExpression: "0 0 * * *", lastRun: lastRun, currentTime: currentTime).Should().BeFalse();
    }

    [Fact]
    public void GetNextOccurrence_DailyExpression_ReturnsNextMidnight()
    {
        DateTime baseTime = new(year: 2025, month: 6, day: 10, hour: 14, minute: 0, second: 0, kind: DateTimeKind.Utc);

        DateTime next = CronService.GetNextOccurrence(cronExpression: "0 0 * * *", baseTime: baseTime);

        next.Should().Be(expected: new DateTime(year: 2025, month: 6, day: 11, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Unspecified));
    }
}
