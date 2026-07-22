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

using NCrontab;
using NoMercyQueue.Core;
using Xunit;

namespace NoMercy.Tests.Queue;

public class CronExpressionBuilderTests
{
    #region Minute Operations Tests

    [Fact]
    public void EveryMinute_SetsMinuteToAsterisk()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryMinute();
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expectedStartString: "*", actualString: result);
    }

    [Theory]
    [InlineData(data: [1, "*/1"])]
    [InlineData(data: [5, "*/5"])]
    [InlineData(data: [15, "*/15"])]
    [InlineData(data: [30, "*/30"])]
    [InlineData(data: [59, "*/59"])]
    public void EveryMinutes_ValidValues_SetsCorrectExpression(int minutes, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryMinutes(minutes: minutes);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expectedStartString: expected, actualString: result);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: 60)]
    [InlineData(data: -1)]
    public void EveryMinutes_InvalidValues_ThrowsArgumentOutOfRangeException(int minutes)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.EveryMinutes(minutes: minutes));
    }

    [Theory]
    [InlineData(data: [0, "0"])]
    [InlineData(data: [15, "15"])]
    [InlineData(data: [30, "30"])]
    [InlineData(data: [59, "59"])]
    public void AtMinute_ValidValues_SetsCorrectMinute(int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtMinute(minute: minute);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expectedStartString: expected, actualString: result);
    }

    [Theory]
    [InlineData(data: -1)]
    [InlineData(data: 60)]
    public void AtMinute_InvalidValues_ThrowsArgumentOutOfRangeException(int minute)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.AtMinute(minute: minute));
    }

    [Fact]
    public void AtMinutes_ValidValues_SetsCommaSeparatedMinutes()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtMinutes(minutes: [0, 15, 30, 45]);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expectedStartString: "0,15,30,45", actualString: result);
    }

    [Fact]
    public void AtMinutes_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.AtMinutes(minutes: [0, 60, 30]));
    }

    [Theory]
    [InlineData(data: [0, 30, "0-30"])]
    [InlineData(data: [15, 45, "15-45"])]
    public void MinuteRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().MinuteRange(start: start, end: end);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expectedStartString: expected, actualString: result);
    }

    [Theory]
    [InlineData(data: [-1, 30])]
    [InlineData(data: [0, 60])]
    [InlineData(data: [30, 15])]
    [InlineData(data: [30, 30])]
    public void MinuteRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(testCode: () => builder.MinuteRange(start: start, end: end));
    }

    #endregion

    #region Hour Operations Tests

    [Fact]
    public void EveryHour_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryHour();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "0 * * * *", actual: result);
    }

    [Theory]
    [InlineData(data: [1, "0 */1 * * *"])]
    [InlineData(data: [6, "0 */6 * * *"])]
    [InlineData(data: [12, "0 */12 * * *"])]
    [InlineData(data: [23, "0 */23 * * *"])]
    public void EveryHours_ValidValues_SetsCorrectExpression(int hours, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryHours(hours: hours);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: 24)]
    [InlineData(data: -1)]
    public void EveryHours_InvalidValues_ThrowsArgumentOutOfRangeException(int hours)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.EveryHours(hours: hours));
    }

    [Theory]
    [InlineData(data: [0, "0 0 * * *"])]
    [InlineData(data: [12, "0 12 * * *"])]
    [InlineData(data: [23, "0 23 * * *"])]
    public void AtHour_ValidValues_SetsCorrectHour(int hour, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtHour(hour: hour);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: -1)]
    [InlineData(data: 24)]
    public void AtHour_InvalidValues_ThrowsArgumentOutOfRangeException(int hour)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.AtHour(hour: hour));
    }

    [Fact]
    public void AtHours_ValidValues_SetsCommaSeparatedHours()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtHours(hours: [9, 12, 18]);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "0 9,12,18 * * *", actual: result);
    }

    [Fact]
    public void AtHours_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.AtHours(hours: [9, 24, 18]));
    }

    [Theory]
    [InlineData(data: [9, 17, "0 9-17 * * *"])]
    [InlineData(data: [0, 12, "0 0-12 * * *"])]
    public void HourRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().HourRange(start: start, end: end);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: [-1, 12])]
    [InlineData(data: [0, 24])]
    [InlineData(data: [12, 9])]
    [InlineData(data: [12, 12])]
    public void HourRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(testCode: () => builder.HourRange(start: start, end: end));
    }

    #endregion

    #region Day of Month Operations Tests

    [Fact]
    public void EveryDay_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryDay();
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: "* * *", actualString: result);
    }

    [Theory]
    [InlineData(data: 1)]
    [InlineData(data: 15)]
    [InlineData(data: 31)]
    public void OnDay_ValidValues_SetsCorrectDay(int day)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDay(day: day);
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: $" {day} ", actualString: result);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: 32)]
    [InlineData(data: -1)]
    public void OnDay_InvalidValues_ThrowsArgumentOutOfRangeException(int day)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.OnDay(day: day));
    }

    [Fact]
    public void OnDays_ValidValues_SetsCommaSeparatedDays()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDays(days: [1, 15, 31]);
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: "1,15,31", actualString: result);
    }

    [Fact]
    public void OnDays_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.OnDays(days: [1, 32, 15]));
    }

    [Theory]
    [InlineData(data: [1, 15, "1-15"])]
    [InlineData(data: [5, 25, "5-25"])]
    public void DayRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().DayRange(start: start, end: end);
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: expected, actualString: result);
    }

    [Theory]
    [InlineData(data: [0, 15])]
    [InlineData(data: [1, 32])]
    [InlineData(data: [15, 10])]
    [InlineData(data: [15, 15])]
    public void DayRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(testCode: () => builder.DayRange(start: start, end: end));
    }

    [Theory]
    [InlineData(data: [1, "0 0 */1 * *"])]
    [InlineData(data: [7, "0 0 */7 * *"])]
    [InlineData(data: [31, "0 0 */31 * *"])]
    public void EveryNthDay_ValidValues_SetsCorrectExpression(int n, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryNthDay(n: n);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: 32)]
    [InlineData(data: -1)]
    public void EveryNthDay_InvalidValues_ThrowsArgumentOutOfRangeException(int n)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.EveryNthDay(n: n));
    }

    [Fact]
    public void LastDayOfMonth_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().LastDayOfMonth();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "0 0 L * *", actual: result);
    }

    #endregion

    #region Month Operations Tests

    [Fact]
    public void EveryMonth_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryMonth();
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: "* *", actualString: result);
    }

    [Theory]
    [InlineData(data: 1)]
    [InlineData(data: 6)]
    [InlineData(data: 12)]
    public void InMonth_ValidValues_SetsCorrectMonth(int month)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().InMonth(month: month);
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: $" {month} ", actualString: result);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: 13)]
    [InlineData(data: -1)]
    public void InMonth_InvalidValues_ThrowsArgumentOutOfRangeException(int month)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.InMonth(month: month));
    }

    [Fact]
    public void InMonths_ValidValues_SetsCommaSeparatedMonths()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().InMonths(months: [1, 6, 12]);
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: "1,6,12", actualString: result);
    }

    [Fact]
    public void InMonths_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.InMonths(months: [1, 13, 6]));
    }

    [Theory]
    [InlineData(data: [1, 6, "1-6"])]
    [InlineData(data: [3, 9, "3-9"])]
    public void MonthRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().MonthRange(start: start, end: end);
        string result = builder.Build();

        // Assert
        Assert.Contains(expectedSubstring: expected, actualString: result);
    }

    [Theory]
    [InlineData(data: [0, 6])]
    [InlineData(data: [1, 13])]
    [InlineData(data: [6, 3])]
    [InlineData(data: [6, 6])]
    public void MonthRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(testCode: () => builder.MonthRange(start: start, end: end));
    }

    [Theory]
    [InlineData(data: [1, "0 0 1 */1 *"])]
    [InlineData(data: [3, "0 0 1 */3 *"])]
    [InlineData(data: [12, "0 0 1 */12 *"])]
    public void EveryNthMonth_ValidValues_SetsCorrectExpression(int n, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryNthMonth(n: n);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: 13)]
    [InlineData(data: -1)]
    public void EveryNthMonth_InvalidValues_ThrowsArgumentOutOfRangeException(int n)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.EveryNthMonth(n: n));
    }

    #endregion

    #region Day of Week Operations Tests

    [Fact]
    public void AnyDayOfWeek_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AnyDayOfWeek();
        string result = builder.Build();

        // Assert
        Assert.EndsWith(expectedEndString: "*", actualString: result);
    }

    [Theory]
    [InlineData(data: [DayOfWeek.Sunday, "0"])]
    [InlineData(data: [DayOfWeek.Monday, "1"])]
    [InlineData(data: [DayOfWeek.Tuesday, "2"])]
    [InlineData(data: [DayOfWeek.Wednesday, "3"])]
    [InlineData(data: [DayOfWeek.Thursday, "4"])]
    [InlineData(data: [DayOfWeek.Friday, "5"])]
    [InlineData(data: [DayOfWeek.Saturday, "6"])]
    public void OnDayOfWeek_ValidValues_SetsCorrectDayOfWeek(DayOfWeek dayOfWeek, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDayOfWeek(dayOfWeek: dayOfWeek);
        string result = builder.Build();

        // Assert
        Assert.EndsWith(expectedEndString: expected, actualString: result);
    }

    [Fact]
    public void OnDaysOfWeek_ValidValues_SetsCommaSeparatedDays()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDaysOfWeek(daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]
        );
        string result = builder.Build();

        // Assert
        Assert.EndsWith(expectedEndString: "1,3,5", actualString: result);
    }

    [Fact]
    public void Weekdays_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Weekdays();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "0 0 * * 1-5", actual: result);
    }

    [Fact]
    public void Weekends_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Weekends();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "0 0 * * 0,6", actual: result);
    }

    [Theory]
    [InlineData(data: [1, DayOfWeek.Monday, "0 0 * * 1#1"])]
    [InlineData(data: [2, DayOfWeek.Friday, "0 0 * * 5#2"])]
    [InlineData(data: [5, DayOfWeek.Sunday, "0 0 * * 0#5"])]
    public void OnNthDayOfWeek_ValidValues_SetsCorrectExpression(
        int nth,
        DayOfWeek dayOfWeek,
        string expected
    )
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnNthDayOfWeek(nth: nth, dayOfWeek: dayOfWeek);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: [0, DayOfWeek.Monday])]
    [InlineData(data: [6, DayOfWeek.Friday])]
    [InlineData(data: [-1, DayOfWeek.Sunday])]
    public void OnNthDayOfWeek_InvalidValues_ThrowsArgumentOutOfRangeException(
        int nth,
        DayOfWeek dayOfWeek
    )
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.OnNthDayOfWeek(nth: nth, dayOfWeek: dayOfWeek));
    }

    [Theory]
    [InlineData(data: [DayOfWeek.Monday, "0 0 * * 1L"])]
    [InlineData(data: [DayOfWeek.Friday, "0 0 * * 5L"])]
    [InlineData(data: [DayOfWeek.Sunday, "0 0 * * 0L"])]
    public void LastDayOfWeek_ValidValues_SetsCorrectExpression(
        DayOfWeek dayOfWeek,
        string expected
    )
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().LastDayOfWeek(dayOfWeek: dayOfWeek);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    #endregion

    #region Common Patterns Tests

    [Theory]
    [InlineData(data: [0, 0, "0 0 * * *"])]
    [InlineData(data: [12, 30, "30 12 * * *"])]
    [InlineData(data: [23, 59, "59 23 * * *"])]
    public void Daily_ValidValues_SetsCorrectExpression(int hour, int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Daily(hour: hour, minute: minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: [DayOfWeek.Monday, 0, 0, "0 0 * * 1"])]
    [InlineData(data: [DayOfWeek.Friday, 18, 30, "30 18 * * 5"])]
    [InlineData(data: [DayOfWeek.Sunday, 9, 15, "15 9 * * 0"])]
    public void Weekly_ValidValues_SetsCorrectExpression(
        DayOfWeek dayOfWeek,
        int hour,
        int minute,
        string expected
    )
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Weekly(dayOfWeek: dayOfWeek, hour: hour, minute: minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: [1, 0, 0, "0 0 1 * *"])]
    [InlineData(data: [15, 12, 30, "30 12 15 * *"])]
    [InlineData(data: [31, 23, 59, "59 23 31 * *"])]
    public void Monthly_ValidValues_SetsCorrectExpression(
        int day,
        int hour,
        int minute,
        string expected
    )
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Monthly(day: day, hour: hour, minute: minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: [1, 1, 0, 0, "0 0 1 1 *"])]
    [InlineData(data: [12, 25, 18, 30, "30 18 25 12 *"])]
    [InlineData(data: [6, 15, 9, 45, "45 9 15 6 *"])]
    public void Yearly_ValidValues_SetsCorrectExpression(
        int month,
        int day,
        int hour,
        int minute,
        string expected
    )
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Yearly(
            month: month,
            day: day,
            hour: hour,
            minute: minute
        );
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: [0, "0 * * * *"])]
    [InlineData(data: [30, "30 * * * *"])]
    [InlineData(data: [59, "59 * * * *"])]
    public void Hourly_ValidValues_SetsCorrectExpression(int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Hourly(minute: minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: -1)]
    [InlineData(data: 60)]
    public void Hourly_InvalidMinute_ThrowsArgumentOutOfRangeException(int minute)
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Hourly(minute: minute));
    }

    [Theory]
    [InlineData(data: [-1, 0])]
    [InlineData(data: [24, 0])]
    public void Weekly_InvalidHour_ThrowsArgumentOutOfRangeException(int hour, int minute)
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () =>
            builder.Weekly(dayOfWeek: DayOfWeek.Monday, hour: hour, minute: minute)
        );
    }

    [Theory]
    [InlineData(data: [0, -1])]
    [InlineData(data: [0, 60])]
    public void Weekly_InvalidMinute_ThrowsArgumentOutOfRangeException(int hour, int minute)
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () =>
            builder.Weekly(dayOfWeek: DayOfWeek.Monday, hour: hour, minute: minute)
        );
    }

    [Theory]
    [InlineData(data: [0, 0, 0])]
    [InlineData(data: [32, 0, 0])]
    public void Monthly_InvalidDay_ThrowsArgumentOutOfRangeException(int day, int hour, int minute)
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Monthly(day: day, hour: hour, minute: minute));
    }

    [Theory]
    [InlineData(data: [1, -1, 0])]
    [InlineData(data: [1, 24, 0])]
    public void Monthly_InvalidHour_ThrowsArgumentOutOfRangeException(int day, int hour, int minute)
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Monthly(day: day, hour: hour, minute: minute));
    }

    [Theory]
    [InlineData(data: [1, 0, -1])]
    [InlineData(data: [1, 0, 60])]
    public void Monthly_InvalidMinute_ThrowsArgumentOutOfRangeException(
        int day,
        int hour,
        int minute
    )
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Monthly(day: day, hour: hour, minute: minute));
    }

    [Theory]
    [InlineData(data: [0, 1, 0, 0])]
    [InlineData(data: [13, 1, 0, 0])]
    public void Yearly_InvalidMonth_ThrowsArgumentOutOfRangeException(
        int month,
        int day,
        int hour,
        int minute
    )
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Yearly(month: month, day: day, hour: hour, minute: minute));
    }

    [Theory]
    [InlineData(data: [1, 0, 0, 0])]
    [InlineData(data: [1, 32, 0, 0])]
    public void Yearly_InvalidDay_ThrowsArgumentOutOfRangeException(
        int month,
        int day,
        int hour,
        int minute
    )
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Yearly(month: month, day: day, hour: hour, minute: minute));
    }

    [Theory]
    [InlineData(data: [1, 1, -1, 0])]
    [InlineData(data: [1, 1, 24, 0])]
    public void Yearly_InvalidHour_ThrowsArgumentOutOfRangeException(
        int month,
        int day,
        int hour,
        int minute
    )
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Yearly(month: month, day: day, hour: hour, minute: minute));
    }

    [Theory]
    [InlineData(data: [1, 1, 0, -1])]
    [InlineData(data: [1, 1, 0, 60])]
    public void Yearly_InvalidMinute_ThrowsArgumentOutOfRangeException(
        int month,
        int day,
        int hour,
        int minute
    )
    {
        CronExpressionBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(testCode: () => builder.Yearly(month: month, day: day, hour: hour, minute: minute));
    }

    #endregion

    #region Builder Pattern Tests

    [Fact]
    public void Build_ReturnsCorrectCronExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "* * * * *", actual: result);
    }

    [Fact]
    public void ToString_ReturnsCorrectCronExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtMinute(minute: 30).AtHour(hour: 12);
        string result = builder.ToString();

        // Assert - AtHour sets minute to "0", overriding the previous AtMinute(30)
        Assert.Equal(expected: "0 12 * * *", actual: result);
    }

    [Fact]
    public void ImplicitStringConversion_ReturnsCorrectCronExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Daily(hour: 9, minute: 30);
        string result = builder;

        // Assert
        Assert.Equal(expected: "30 9 * * *", actual: result);
    }

    [Fact]
    public void Reset_ResetsAllFieldsToDefault()
    {
        // Arrange
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinute(minute: 30)
            .AtHour(hour: 12)
            .OnDay(day: 15)
            .InMonth(month: 6)
            .OnDayOfWeek(dayOfWeek: DayOfWeek.Friday);

        // Act
        builder.Reset();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "* * * * *", actual: result);
    }

    #endregion

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterface_ChainMultipleMethods_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinute(minute: 30)
            .AtHour(hour: 14)
            .OnDay(day: 15)
            .InMonth(month: 3)
            .OnDayOfWeek(dayOfWeek: DayOfWeek.Friday);

        string result = builder.Build();

        // Assert - AtHour sets minute to "0", overriding the previous AtMinute(30)
        Assert.Equal(expected: "0 14 15 3 5", actual: result);
    }

    [Fact]
    public void FluentInterface_OverwritePreviousValues_UsesLatestValues()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinute(minute: 15)
            .AtMinute(minute: 30) // This should overwrite the previous value
            .AtHour(hour: 9)
            .AtHour(hour: 14); // This should overwrite the previous value and set minute to "0"

        string result = builder.Build();

        // Assert - AtHour methods set minute to "0", so final minute will be "0"
        Assert.Equal(expected: "0 14 * * *", actual: result);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void ComplexExpression_CombineMultipleFeatures_BuildsCorrectly()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinutes(minutes: [0, 30])
            .AtHours(hours: [9, 12, 18])
            .OnDays(days: [1, 15])
            .InMonths(months: [1, 6, 12])
            .OnDaysOfWeek(daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);

        string result = builder.Build();

        // Assert - AtHours overrides the minutes, so it will be "0" not "0,30"
        Assert.Equal(expected: "0 9,12,18 1,15 1,6,12 1,3,5", actual: result);
    }

    [Fact]
    public void DefaultValues_BuildWithoutSettingAnyValues_ReturnsAllAsterisks()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new();
        string result = builder.Build();

        // Assert
        Assert.Equal(expected: "* * * * *", actual: result);
    }

    [Fact]
    public void MethodChaining_AllMethodsReturnBuilder_AllowsFluentInterface()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new();
        CronExpressionBuilder result = builder
            .EveryMinute()
            .EveryHour()
            .EveryDay()
            .EveryMonth()
            .AnyDayOfWeek();

        // Assert
        Assert.IsType<CronExpressionBuilder>(@object: result);
        // EveryHour sets minute to "0", so final result will be "0 * * * *"
        Assert.Equal(expected: "0 * * * *", actual: result.Build());
    }

    #endregion

    #region Integration Tests with NCrontab

    // These integration tests verify that the CronExpressionBuilder generates valid cron expressions
    // that work correctly with NCrontab library for actual date/time scheduling.
    // They test both string generation and real-world time matching scenarios.

    [Fact]
    public void Daily_CronExpression_MatchesExpectedTimes()
    {
        // Arrange
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Daily(hour: 14, minute: 30); // 2:30 PM daily
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(year: 2025, month: 9, day: 3, hour: 14, minute: 30, second: 0); // Same day 2:30 PM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);

        // Assert
        Assert.Equal(expected: expectedTime, actual: nextOccurrence);

        // Test multiple occurrences
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddDays(value: 3))
            .Take(count: 3)
            .ToList();
        Assert.Equal(expected: 3, actual: occurrences.Count);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 30, second: 0), actual: occurrences[index: 0]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 4, hour: 14, minute: 30, second: 0), actual: occurrences[index: 1]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 5, hour: 14, minute: 30, second: 0), actual: occurrences[index: 2]);
    }

    [Fact]
    public void Weekly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Every Friday at 6:00 PM
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Weekly(
            dayOfWeek: DayOfWeek.Friday,
            hour: 18
        );
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Wednesday, Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(year: 2025, month: 9, day: 5, hour: 18, minute: 0, second: 0); // Friday, Sep 5, 2025 6:00 PM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);

        // Assert
        Assert.Equal(expected: expectedTime, actual: nextOccurrence);
        Assert.Equal(expected: DayOfWeek.Friday, actual: nextOccurrence.DayOfWeek);

        // Test next week
        DateTime nextWeekOccurrence = schedule.GetNextOccurrence(baseTime: nextOccurrence);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 12, hour: 18, minute: 0, second: 0), actual: nextWeekOccurrence);
        Assert.Equal(expected: DayOfWeek.Friday, actual: nextWeekOccurrence.DayOfWeek);
    }

    [Fact]
    public void Monthly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - 15th of every month at 9:15 AM
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Monthly(day: 15, hour: 9, minute: 15);
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(year: 2025, month: 9, day: 15, hour: 9, minute: 15, second: 0); // Sep 15, 2025 9:15 AM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);

        // Assert
        Assert.Equal(expected: expectedTime, actual: nextOccurrence);
        Assert.Equal(expected: 15, actual: nextOccurrence.Day);

        // Test next month
        DateTime nextMonthOccurrence = schedule.GetNextOccurrence(baseTime: nextOccurrence);
        Assert.Equal(expected: new(year: 2025, month: 10, day: 15, hour: 9, minute: 15, second: 0), actual: nextMonthOccurrence);
        Assert.Equal(expected: 15, actual: nextMonthOccurrence.Day);
    }

    [Fact]
    public void Yearly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - December 25th at 12:00 PM
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Yearly(month: 12, day: 25, hour: 12);
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(year: 2025, month: 12, day: 25, hour: 12, minute: 0, second: 0); // Dec 25, 2025 12:00 PM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);

        // Assert
        Assert.Equal(expected: expectedTime, actual: nextOccurrence);
        Assert.Equal(expected: 12, actual: nextOccurrence.Month);
        Assert.Equal(expected: 25, actual: nextOccurrence.Day);

        // Test next year
        DateTime nextYearOccurrence = schedule.GetNextOccurrence(baseTime: nextOccurrence);
        Assert.Equal(expected: new(year: 2026, month: 12, day: 25, hour: 12, minute: 0, second: 0), actual: nextYearOccurrence);
    }

    [Fact]
    public void Hourly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Every hour at 45 minutes
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Hourly(minute: 45);
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 14, minute: 30, second: 0); // Sep 3, 2025 2:30 PM
        DateTime expectedTime = new(year: 2025, month: 9, day: 3, hour: 14, minute: 45, second: 0); // Same hour at 45 minutes
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);

        // Assert
        Assert.Equal(expected: expectedTime, actual: nextOccurrence);
        Assert.Equal(expected: 45, actual: nextOccurrence.Minute);

        // Test next hour
        DateTime nextHourOccurrence = schedule.GetNextOccurrence(baseTime: nextOccurrence);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 15, minute: 45, second: 0), actual: nextHourOccurrence);
        Assert.Equal(expected: 45, actual: nextHourOccurrence.Minute);
    }

    [Fact]
    public void EveryMinutes_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Every 15 minutes
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().EveryMinutes(minutes: 15);
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 14, minute: 7, second: 0); // Sep 3, 2025 2:07 PM
        DateTime expectedTime = new(year: 2025, month: 9, day: 3, hour: 14, minute: 15, second: 0); // Next 15-minute mark
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);

        // Assert
        Assert.Equal(expected: expectedTime, actual: nextOccurrence);

        // Test multiple occurrences
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddHours(value: 2))
            .Take(count: 5)
            .ToList();
        Assert.Equal(expected: 5, actual: occurrences.Count);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 15, second: 0), actual: occurrences[index: 0]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 30, second: 0), actual: occurrences[index: 1]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 45, second: 0), actual: occurrences[index: 2]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 15, minute: 0, second: 0), actual: occurrences[index: 3]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 15, minute: 15, second: 0), actual: occurrences[index: 4]);
    }

    [Fact]
    public void Weekdays_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Weekdays (Monday-Friday) at midnight
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Weekdays();
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Wednesday, Sep 3, 2025 10:00 AM

        // Get next few occurrences
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddDays(value: 10))
            .Take(count: 5)
            .ToList();

        // Assert all are weekdays at midnight
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(
                condition: occurrence.DayOfWeek != DayOfWeek.Saturday
                           && occurrence.DayOfWeek != DayOfWeek.Sunday
            );
            Assert.Equal(expected: 0, actual: occurrence.Hour);
            Assert.Equal(expected: 0, actual: occurrence.Minute);
        }

        // Verify specific dates
        Assert.Equal(expected: new(year: 2025, month: 9, day: 4, hour: 0, minute: 0, second: 0), actual: occurrences[index: 0]); // Thursday
        Assert.Equal(expected: new(year: 2025, month: 9, day: 5, hour: 0, minute: 0, second: 0), actual: occurrences[index: 1]); // Friday
        Assert.Equal(expected: new(year: 2025, month: 9, day: 8, hour: 0, minute: 0, second: 0), actual: occurrences[index: 2]); // Monday (skips weekend)
    }

    [Fact]
    public void Weekends_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Weekends (Saturday & Sunday) at midnight
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Weekends();
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Wednesday, Sep 3, 2025 10:00 AM

        // Get next few occurrences over a longer period to ensure we get weekend days
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddDays(value: 15))
            .Take(count: 4)
            .ToList();

        // Assert all are weekends at midnight
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(
                condition: occurrence.DayOfWeek == DayOfWeek.Saturday
                           || occurrence.DayOfWeek == DayOfWeek.Sunday
            );
            Assert.Equal(expected: 0, actual: occurrence.Hour);
            Assert.Equal(expected: 0, actual: occurrence.Minute);
        }

        // Verify we have at least 4 occurrences
        Assert.True(condition: occurrences.Count >= 4);

        // Verify we get both Saturday and Sunday
        List<DateTime> saturdays = occurrences
            .Where(predicate: o => o.DayOfWeek == DayOfWeek.Saturday)
            .ToList();
        List<DateTime> sundays = occurrences.Where(predicate: o => o.DayOfWeek == DayOfWeek.Sunday).ToList();
        Assert.True(condition: saturdays.Count > 0);
        Assert.True(condition: sundays.Count > 0);
    }

    [Fact]
    public void AtMinutes_MultipleValues_MatchesExpectedTimes()
    {
        // Arrange - At minutes 0, 15, 30, 45 of every hour
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().AtMinutes(minutes: [0, 15, 30, 45]);
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 14, minute: 7, second: 0); // Sep 3, 2025 2:07 PM

        // Get next few occurrences
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddHours(value: 2))
            .Take(count: 6)
            .ToList();

        // Assert correct minutes
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 15, second: 0), actual: occurrences[index: 0]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 30, second: 0), actual: occurrences[index: 1]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 14, minute: 45, second: 0), actual: occurrences[index: 2]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 15, minute: 0, second: 0), actual: occurrences[index: 3]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 15, minute: 15, second: 0), actual: occurrences[index: 4]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 15, minute: 30, second: 0), actual: occurrences[index: 5]);
    }

    [Fact]
    public void AtHours_MultipleValues_MatchesExpectedTimes()
    {
        // Arrange - At hours 9, 12, 18 (9 AM, 12 PM, 6 PM) at minute 0
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().AtHours(hours: [9, 12, 18]);
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Sep 3, 2025 10:00 AM

        // Get next few occurrences
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddDays(value: 2))
            .Take(count: 5)
            .ToList();

        // Assert correct hours and minute is always 0
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 12, minute: 0, second: 0), actual: occurrences[index: 0]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 3, hour: 18, minute: 0, second: 0), actual: occurrences[index: 1]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 4, hour: 9, minute: 0, second: 0), actual: occurrences[index: 2]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 4, hour: 12, minute: 0, second: 0), actual: occurrences[index: 3]);
        Assert.Equal(expected: new(year: 2025, month: 9, day: 4, hour: 18, minute: 0, second: 0), actual: occurrences[index: 4]);

        foreach (DateTime occurrence in occurrences)
        {
            Assert.Equal(expected: 0, actual: occurrence.Minute);
            Assert.Contains(expected: occurrence.Hour, collection: new[] { 9, 12, 18 });
        }
    }

    [Fact]
    public void OnDaysOfWeek_MultipleValues_MatchesExpectedTimes()
    {
        // Arrange - Monday, Wednesday, Friday (any time - uses current minute/hour settings)
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().OnDaysOfWeek(daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]
        );
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0); // Wednesday, Sep 3, 2025 10:00 AM

        // Since the expression is "* * * * 1,3,5", it runs every minute on those days
        // Get just a few occurrences to test the day pattern
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddHours(value: 1))
            .Take(count: 10)
            .ToList();

        // Assert correct days of week
        DayOfWeek[] expectedDays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
        foreach (DateTime occurrence in occurrences)
        {
            Assert.Contains(expected: occurrence.DayOfWeek, collection: expectedDays);
        }

        // All occurrences should be on Wednesday since we're only looking at one hour
        Assert.True(condition: occurrences.All(predicate: o => o.DayOfWeek == DayOfWeek.Wednesday));

        // Should get many occurrences (every minute)
        Assert.Equal(expected: 10, actual: occurrences.Count);
    }

    [Fact]
    public void ComplexCronExpression_Integration_MatchesExpectedTimes()
    {
        // Arrange - The complex expression has conflicting settings: AtMinutes vs HourRange vs Weekdays
        // HourRange sets minute to "0", Weekdays sets minute to "0", hour to "0", dayOfWeek to "1-5"
        // So the final result will be: "0 9-17 * * 1-5" (every hour from 9-17 on weekdays at minute 0)
        CronExpressionBuilder cronExpression = new CronExpressionBuilder()
            .AtMinutes(minutes: [0, 30]) // This will be overridden
            .HourRange(start: 9, end: 17) // This sets minute to "0" and hour to "9-17"
            .Weekdays(); // This sets minute to "0", hour to "0", dayOfWeek to "1-5" - overriding hour!

        // The final expression should be: "0 0 * * 1-5" (midnight on weekdays only)
        Assert.Equal(expected: "0 0 * * 1-5", actual: cronExpression.Build());

        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 8, minute: 0, second: 0); // Wednesday, Sep 3, 2025 8:00 AM

        // Get occurrences for a few days
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddDays(value: 3))
            .ToList();

        // Should only occur on weekdays at midnight
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(
                condition: occurrence.DayOfWeek != DayOfWeek.Saturday
                           && occurrence.DayOfWeek != DayOfWeek.Sunday
            );
            Assert.Equal(expected: 0, actual: occurrence.Hour); // Midnight
            Assert.Equal(expected: 0, actual: occurrence.Minute);
        }

        // First occurrence should be midnight on the same day (Wednesday)
        Assert.Equal(expected: new(year: 2025, month: 9, day: 4, hour: 0, minute: 0, second: 0), actual: occurrences[index: 0]); // Thursday midnight
    }

    [Fact]
    public void ProperComplexCronExpression_BusinessHours_MatchesExpectedTimes()
    {
        // Arrange - Expression: "0 9,10,11,12,13,14,15,16,17 * * 1,2,3,4,5"
        // AtHours overrides AtMinutes, so final minute is "0"
        CronExpressionBuilder cronExpression = new CronExpressionBuilder()
            .AtMinutes(minutes: [0, 30]) // This gets overridden by AtHours
            .AtHours(hours: [9, 10, 11, 12, 13, 14, 15, 16, 17]) // Sets minute to "0"
            .OnDaysOfWeek(daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
            );

        // The expression should be "0 9,10,11,12,13,14,15,16,17 * * 1,2,3,4,5"
        Assert.Equal(expected: "0 9,10,11,12,13,14,15,16,17 * * 1,2,3,4,5", actual: cronExpression.Build());

        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);

        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 8, minute: 0, second: 0); // Wednesday, Sep 3, 2025 8:00 AM

        // Get occurrences for one day
        List<DateTime> occurrences = schedule
            .GetNextOccurrences(baseTime: baseTime, endTime: baseTime.AddDays(value: 1))
            .Take(count: 20)
            .ToList();

        // Should only occur on weekdays, during business hours, at minute 0
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(
                condition: occurrence.DayOfWeek != DayOfWeek.Saturday
                           && occurrence.DayOfWeek != DayOfWeek.Sunday
            );
            Assert.True(condition: occurrence.Hour is >= 9 and <= 17);
            Assert.Equal(expected: 0, actual: occurrence.Minute); // Always minute 0 due to AtHours override
        }

        // Should have 9 occurrences for Wednesday (9 AM to 5 PM)
        List<DateTime> wednesdayOccurrences = occurrences
            .Where(predicate: o => o.DayOfWeek == DayOfWeek.Wednesday)
            .ToList();
        Assert.Equal(expected: 9, actual: wednesdayOccurrences.Count);
    }

    [Fact]
    public void InvalidCronExpression_ThrowsCrontabException()
    {
        // Arrange - Create an invalid cron expression manually
        string invalidCron = "invalid cron expression";

        // Act & Assert - NCrontab throws CrontabException, not FormatException
        Assert.Throws<CrontabException>(testCode: () => CrontabSchedule.Parse(expression: invalidCron));
    }

    [Theory]
    [InlineData(data: "0 0 * * *")]
    [InlineData(data: "30 14 * * *")]
    [InlineData(data: "0 9 * * 1")]
    [InlineData(data: "0 0 1 * *")]
    [InlineData(data: "0 0 1 1 *")]
    [InlineData(data: "*/15 * * * *")]
    [InlineData(data: "0 */2 * * *")]
    [InlineData(data: "0 9-17 * * 1-5")]
    public void ValidCronExpressions_CanBeParsedByNCrontab(string cronExpression)
    {
        // Act & Assert - Should not throw
        CrontabSchedule? schedule = CrontabSchedule.Parse(expression: cronExpression);
        Assert.NotNull(@object: schedule);

        // Verify it can calculate next occurrence
        DateTime baseTime = new(year: 2025, month: 9, day: 3, hour: 10, minute: 0, second: 0);
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime: baseTime);
        Assert.True(condition: nextOccurrence > baseTime);
    }

    #endregion
}
