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

using NoMercy.Api.WebSockets;
using Xunit;

namespace NoMercy.Tests.Api.WebSockets;

/// <summary>
/// The broadcast loop's delay used to sit inside the try, so a persistent
/// Monitor()/send failure skipped it and respun the loop at CPU speed. NextDelayMs
/// guarantees a full-interval backoff on failure.
/// </summary>
public class ResourceMonitorServiceDelayTests
{
    [Theory]
    [InlineData(data: 5)]
    [InlineData(data: 900)]
    [InlineData(data: 5000)]
    public void NextDelayMs_OnFailure_AlwaysBacksOffFullInterval(int elapsedMs)
    {
        Assert.Equal(expected: 1000, actual: ResourceMonitorService.NextDelayMs(failed: true, elapsedMs: elapsedMs, intervalMs: 1000));
    }

    [Fact]
    public void NextDelayMs_OnSuccess_PacesDownByElapsed()
    {
        Assert.Equal(expected: 800, actual: ResourceMonitorService.NextDelayMs(failed: false, elapsedMs: 200, intervalMs: 1000));
    }

    [Fact]
    public void NextDelayMs_OnSuccess_WhenWorkOutranInterval_IsNonPositive()
    {
        Assert.True(condition: ResourceMonitorService.NextDelayMs(failed: false, elapsedMs: 1500, intervalMs: 1000) <= 0);
    }
}
