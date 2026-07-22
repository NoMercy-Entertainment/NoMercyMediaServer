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

using NoMercy.Launcher.Services;
using Xunit;

namespace NoMercy.Tests.Service;

public class TrayIconManagerTests
{
    [Fact]
    public void FormatUptime_Seconds_ReturnsMinutesAndSeconds()
    {
        string result = TrayIconManager.FormatUptime(totalSeconds: 45);
        Assert.Equal(expected: "0m 45s", actual: result);
    }

    [Fact]
    public void FormatUptime_Minutes_ReturnsMinutesAndSeconds()
    {
        string result = TrayIconManager.FormatUptime(totalSeconds: 125);
        Assert.Equal(expected: "2m 5s", actual: result);
    }

    [Fact]
    public void FormatUptime_Hours_ReturnsHoursAndMinutes()
    {
        string result = TrayIconManager.FormatUptime(totalSeconds: 3725);
        Assert.Equal(expected: "1h 2m", actual: result);
    }

    [Fact]
    public void FormatUptime_Days_ReturnsDaysHoursMinutes()
    {
        long totalSeconds = 2 * 86400 + 5 * 3600 + 30 * 60;
        string result = TrayIconManager.FormatUptime(totalSeconds: totalSeconds);
        Assert.Equal(expected: "2d 5h 30m", actual: result);
    }

    [Fact]
    public void FormatUptime_Zero_ReturnsZeroMinutesZeroSeconds()
    {
        string result = TrayIconManager.FormatUptime(totalSeconds: 0);
        Assert.Equal(expected: "0m 0s", actual: result);
    }

    [Fact]
    public void FormatUptime_ExactlyOneHour_ReturnsHoursFormat()
    {
        string result = TrayIconManager.FormatUptime(totalSeconds: 3600);
        Assert.Equal(expected: "1h 0m", actual: result);
    }

    [Fact]
    public void FormatUptime_ExactlyOneDay_ReturnsDaysFormat()
    {
        string result = TrayIconManager.FormatUptime(totalSeconds: 86400);
        Assert.Equal(expected: "1d 0h 0m", actual: result);
    }
}
