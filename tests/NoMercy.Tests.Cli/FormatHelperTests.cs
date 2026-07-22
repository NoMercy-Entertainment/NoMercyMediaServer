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

using NoMercy.Cli.Commands;
using Xunit;

namespace NoMercy.Tests.Cli;

public class FormatHelperTests
{
    [Theory]
    [InlineData(data: [90, "1m 30s"])]
    [InlineData(data: [3661, "1h 1m"])]
    [InlineData(data: [90061, "1d 1h 1m"])]
    [InlineData(data: [30, "0m 30s"])]
    [InlineData(data: [0, "0m 0s"])]
    [InlineData(data: [86400, "1d 0h 0m"])]
    public void FormatUptime_FormatsCorrectly(long totalSeconds, string expected)
    {
        TimeSpan uptime = TimeSpan.FromSeconds(seconds: totalSeconds);
        string result = StatusCommand.FormatUptime(uptime: uptime);
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: ["serverName", "server_name"])]
    [InlineData(data: ["server_name", "server_name"])]
    [InlineData(data: ["server-name", "server_name"])]
    [InlineData(data: ["queueWorkers", "queue_workers"])]
    [InlineData(data: ["", ""])]
    [InlineData(data: ["a", "a"])]
    [InlineData(data: ["ABC", "a_b_c"])]
    public void ToSnakeCase_ConvertsCorrectly(string input, string expected)
    {
        string result = ConfigCommand.ToSnakeCase(input: input);
        Assert.Equal(expected: expected, actual: result);
    }
}
