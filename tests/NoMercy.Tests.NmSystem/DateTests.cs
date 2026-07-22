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
[Trait(name: "Category", value: "Unit")]
public class DateTests
{
    [Theory]
    [InlineData(data: ["2009", 2009, 1, 1])]
    [InlineData(data: ["2009-05-04", 2009, 5, 4])]
    [InlineData(data: ["05/04/2009", 2009, 5, 4])]
    [InlineData(data: ["04-05-2009", 2009, 5, 4])] // dd-MM-yyyy
    public void TryParseToDateTime_ParsesSupportedFormats(
        string input,
        int year,
        int month,
        int day
    )
    {
        bool ok = input.TryParseToDateTime(dateTime: out DateTime result);

        ok.Should().BeTrue();
        result.Year.Should().Be(expected: year);
        result.Month.Should().Be(expected: month);
        result.Day.Should().Be(expected: day);
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "not a date")]
    [InlineData(data: "99-99-9999")]
    public void TryParseToDateTime_RejectsInvalidInput(string input)
    {
        input.TryParseToDateTime(dateTime: out DateTime _).Should().BeFalse();
    }

    [Fact]
    public void SubDays_SubtractsWholeDays()
    {
        new DateTime(year: 2009, month: 5, day: 10).SubDays(days: 4).Should().Be(expected: new(year: 2009, month: 5, day: 6));
    }

    [Fact]
    public void SubDays_ThrowsOnNegative()
    {
        Action act = () => new DateTime(year: 2009, month: 5, day: 10).SubDays(days: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseYear_ReturnsYearForValue()
    {
        new DateTime(year: 2009, month: 5, day: 4).ParseYear().Should().Be(expected: 2009);
    }

    [Fact]
    public void ParseYear_NullableNullReturnsZero()
    {
        DateTime? value = null;
        value.ParseYear().Should().Be(expected: 0);
    }

    [Fact]
    public void ToHms_FormatsSecondsAsTimespan()
    {
        string result = (3661).ToHms();
        result.Should().Contain(expected: ":");
        result.Should().Contain(expected: "01:01:01");
    }

    [Theory]
    [InlineData(data: 0d)]
    [InlineData(data: 3661.5d)]
    public void ToHis_Double_FormatsSecondsWithMilliseconds(double seconds)
    {
        string result = seconds.ToHis();
        result.Should().Contain(expected: ":");
    }

    [Theory]
    [InlineData(data: 0L)]
    [InlineData(data: 3661L)]
    public void ToHis_Long_FormatsSecondsWithMilliseconds(long seconds)
    {
        string result = seconds.ToHis();
        result.Should().Contain(expected: ":");
    }

    [Fact]
    public void ToHumanTime_Int_FormatsSecondsLessThanHour()
    {
        string result = (45).ToHumanTime();
        result.Should().Be(expected: "00:45");
    }

    [Fact]
    public void ToHumanTime_Int_FormatsSecondsGreaterThanHour()
    {
        string result = (3661).ToHumanTime();
        result.Should().Contain(expected: ":");
    }

    [Fact]
    public void ToHumanTime_Double_FormatsSecondsLessThanHour()
    {
        string result = (45d).ToHumanTime();
        result.Should().Be(expected: "00:45");
    }

    [Fact]
    public void ToHumanTime_Double_FormatsSecondsGreaterThanHour()
    {
        string result = (3661d).ToHumanTime();
        result.Should().Contain(expected: ":");
    }

    [Fact]
    public void ToHumanTime_Long_FormatsSecondsLessThanHour()
    {
        string result = (45L).ToHumanTime();
        result.Should().Be(expected: "00:45");
    }

    [Fact]
    public void ToHumanTime_Long_FormatsSecondsGreaterThanHour()
    {
        string result = (3661L).ToHumanTime();
        result.Should().Contain(expected: ":");
    }
}
