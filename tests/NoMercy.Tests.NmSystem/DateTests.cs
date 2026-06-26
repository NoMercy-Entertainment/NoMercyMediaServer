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

/// <summary>
/// Pins <see cref="Date"/>: multi-format date parsing used when ingesting
/// provider metadata, the positive-only <see cref="Date.SubDays"/> guard, and
/// year extraction.
/// </summary>
[Trait("Category", "Unit")]
public class DateTests
{
    [Theory]
    [InlineData("2009", 2009, 1, 1)]
    [InlineData("2009-05-04", 2009, 5, 4)]
    [InlineData("05/04/2009", 2009, 5, 4)]
    [InlineData("04-05-2009", 2009, 5, 4)] // dd-MM-yyyy
    public void TryParseToDateTime_ParsesSupportedFormats(
        string input,
        int year,
        int month,
        int day
    )
    {
        bool ok = input.TryParseToDateTime(out DateTime result);

        ok.Should().BeTrue();
        result.Year.Should().Be(year);
        result.Month.Should().Be(month);
        result.Day.Should().Be(day);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("99-99-9999")]
    public void TryParseToDateTime_RejectsInvalidInput(string input)
    {
        input.TryParseToDateTime(out DateTime _).Should().BeFalse();
    }

    [Fact]
    public void SubDays_SubtractsWholeDays()
    {
        new DateTime(2009, 5, 10).SubDays(4).Should().Be(new DateTime(2009, 5, 6));
    }

    [Fact]
    public void SubDays_ThrowsOnNegative()
    {
        Action act = () => new DateTime(2009, 5, 10).SubDays(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseYear_ReturnsYearForValue()
    {
        new DateTime(2009, 5, 4).ParseYear().Should().Be(2009);
    }

    [Fact]
    public void ParseYear_NullableNullReturnsZero()
    {
        DateTime? value = null;
        value.ParseYear().Should().Be(0);
    }
}
