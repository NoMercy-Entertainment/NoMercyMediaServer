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
    [InlineData(5)]
    [InlineData(900)]
    [InlineData(5000)]
    public void NextDelayMs_OnFailure_AlwaysBacksOffFullInterval(int elapsedMs)
    {
        Assert.Equal(1000, ResourceMonitorService.NextDelayMs(true, elapsedMs, 1000));
    }

    [Fact]
    public void NextDelayMs_OnSuccess_PacesDownByElapsed()
    {
        Assert.Equal(800, ResourceMonitorService.NextDelayMs(false, 200, 1000));
    }

    [Fact]
    public void NextDelayMs_OnSuccess_WhenWorkOutranInterval_IsNonPositive()
    {
        Assert.True(ResourceMonitorService.NextDelayMs(false, 1500, 1000) <= 0);
    }
}
