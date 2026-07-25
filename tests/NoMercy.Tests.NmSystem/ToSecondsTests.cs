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

namespace NoMercy.Tests.NmSystem;

[Trait("Category", "Unit")]
public class ToSecondsTests
{
    [Theory]
    [InlineData(["45", 45])]
    [InlineData(["00", 0])]
    [InlineData(["05.500", 5])]
    public void ToSeconds_SingleSegment_ReturnsSeconds(string input, int expected)
    {
        input.ToSeconds().Should().Be(expected);
    }

    [Theory]
    [InlineData(["02:30", 150])]
    [InlineData(["00:05", 5])]
    [InlineData(["10:00", 600])]
    public void ToSeconds_TwoSegments_ReturnsMinutesAndSeconds(string input, int expected)
    {
        input.ToSeconds().Should().Be(expected);
    }

    [Theory]
    [InlineData(["01:02:03", 3723])]
    [InlineData(["00:00:00", 0])]
    [InlineData(["02:00:00.250", 7200])]
    public void ToSeconds_ThreeSegments_ReturnsHoursMinutesAndSeconds(string input, int expected)
    {
        input.ToSeconds().Should().Be(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ab:cd")]
    [InlineData("ab:cd:ef")]
    [InlineData(":")]
    [InlineData("::")]
    public void ToSeconds_MalformedInput_ReturnsZeroInsteadOfThrowing(string input)
    {
        Action act = () => input.ToSeconds();
        act.Should().NotThrow();
        input.ToSeconds().Should().Be(0);
    }

    [Fact]
    public void ToSeconds_NullOrEmpty_ReturnsZero()
    {
        ((string?)null).ToSeconds().Should().Be(0);
        string.Empty.ToSeconds().Should().Be(0);
    }
}
