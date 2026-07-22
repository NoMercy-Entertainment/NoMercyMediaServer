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

using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginAbiTests
{
    [Theory]
    [InlineData(data: [null, true])]
    [InlineData(data: ["", true])]
    [InlineData(data: ["10.0", true])]
    [InlineData(data: ["10.1", false])]
    [InlineData(data: ["9.0", false])]
    [InlineData(data: ["9.5", false])]
    [InlineData(data: ["11.0", false])]
    [InlineData(data: ["not-a-version", false])]
    public void IsCompatible_AppliesMajorMatchMinorCeiling(string? targetAbi, bool expected)
    {
        Assert.Equal(expected: expected, actual: PluginAbi.IsCompatible(targetAbi: targetAbi));
    }

    [Fact]
    public void Current_IsTenZero()
    {
        Assert.Equal(expected: new Version(major: 10, minor: 0), actual: PluginAbi.Current);
    }
}
