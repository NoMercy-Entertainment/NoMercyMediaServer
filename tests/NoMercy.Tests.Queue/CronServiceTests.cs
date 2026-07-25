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
[Trait("Category", "Unit")]
public class CronServiceTests
{
    [Fact]
    public void ShouldRun_CurrentTimeAtOrAfterNextOccurrence_ReturnsTrue()
    {
        DateTime lastRun = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime currentTime = new(2025, 1, 2, 0, 0, 1, DateTimeKind.Utc);

        CronService.ShouldRun("0 0 * * *", lastRun, currentTime).Should().BeTrue();
    }

    [Fact]
    public void ShouldRun_CurrentTimeBeforeNextOccurrence_ReturnsFalse()
    {
        DateTime lastRun = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime currentTime = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        CronService.ShouldRun("0 0 * * *", lastRun, currentTime).Should().BeFalse();
    }

    [Fact]
    public void GetNextOccurrence_DailyExpression_ReturnsNextMidnight()
    {
        DateTime baseTime = new(2025, 6, 10, 14, 0, 0, DateTimeKind.Utc);

        DateTime next = CronService.GetNextOccurrence("0 0 * * *", baseTime);

        next.Should().Be(new DateTime(2025, 6, 11, 0, 0, 0, DateTimeKind.Unspecified));
    }
}
