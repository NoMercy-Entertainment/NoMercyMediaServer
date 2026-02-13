using Xunit;
using NoMercy.Queue;
using NCrontab;

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
        Assert.StartsWith("*", result);
    }

    [Theory]
    [InlineData(1, "*/1")]
    [InlineData(5, "*/5")]
    [InlineData(15, "*/15")]
    [InlineData(30, "*/30")]
    [InlineData(59, "*/59")]
    public void EveryMinutes_ValidValues_SetsCorrectExpression(int minutes, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryMinutes(minutes);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(-1)]
    public void EveryMinutes_InvalidValues_ThrowsArgumentOutOfRangeException(int minutes)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.EveryMinutes(minutes));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(15, "15")]
    [InlineData(30, "30")]
    [InlineData(59, "59")]
    public void AtMinute_ValidValues_SetsCorrectMinute(int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtMinute(minute);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expected, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60)]
    public void AtMinute_InvalidValues_ThrowsArgumentOutOfRangeException(int minute)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AtMinute(minute));
    }

    [Fact]
    public void AtMinutes_ValidValues_SetsCommaSeparatedMinutes()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtMinutes(0, 15, 30, 45);
        string result = builder.Build();

        // Assert
        Assert.StartsWith("0,15,30,45", result);
    }

    [Fact]
    public void AtMinutes_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AtMinutes(0, 60, 30));
    }

    [Theory]
    [InlineData(0, 30, "0-30")]
    [InlineData(15, 45, "15-45")]
    public void MinuteRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().MinuteRange(start, end);
        string result = builder.Build();

        // Assert
        Assert.StartsWith(expected, result);
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(0, 60)]
    [InlineData(30, 15)]
    [InlineData(30, 30)]
    public void MinuteRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.MinuteRange(start, end));
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
        Assert.Equal("0 * * * *", result);
    }

    [Theory]
    [InlineData(1, "0 */1 * * *")]
    [InlineData(6, "0 */6 * * *")]
    [InlineData(12, "0 */12 * * *")]
    [InlineData(23, "0 */23 * * *")]
    public void EveryHours_ValidValues_SetsCorrectExpression(int hours, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryHours(hours);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(-1)]
    public void EveryHours_InvalidValues_ThrowsArgumentOutOfRangeException(int hours)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.EveryHours(hours));
    }

    [Theory]
    [InlineData(0, "0 0 * * *")]
    [InlineData(12, "0 12 * * *")]
    [InlineData(23, "0 23 * * *")]
    public void AtHour_ValidValues_SetsCorrectHour(int hour, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtHour(hour);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void AtHour_InvalidValues_ThrowsArgumentOutOfRangeException(int hour)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AtHour(hour));
    }

    [Fact]
    public void AtHours_ValidValues_SetsCommaSeparatedHours()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtHours(9, 12, 18);
        string result = builder.Build();

        // Assert
        Assert.Equal("0 9,12,18 * * *", result);
    }

    [Fact]
    public void AtHours_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AtHours(9, 24, 18));
    }

    [Theory]
    [InlineData(9, 17, "0 9-17 * * *")]
    [InlineData(0, 12, "0 0-12 * * *")]
    public void HourRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().HourRange(start, end);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1, 12)]
    [InlineData(0, 24)]
    [InlineData(12, 9)]
    [InlineData(12, 12)]
    public void HourRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.HourRange(start, end));
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
        Assert.Contains("* * *", result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(31)]
    public void OnDay_ValidValues_SetsCorrectDay(int day)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDay(day);
        string result = builder.Build();

        // Assert
        Assert.Contains($" {day} ", result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(-1)]
    public void OnDay_InvalidValues_ThrowsArgumentOutOfRangeException(int day)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.OnDay(day));
    }

    [Fact]
    public void OnDays_ValidValues_SetsCommaSeparatedDays()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDays(1, 15, 31);
        string result = builder.Build();

        // Assert
        Assert.Contains("1,15,31", result);
    }

    [Fact]
    public void OnDays_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.OnDays(1, 32, 15));
    }

    [Theory]
    [InlineData(1, 15, "1-15")]
    [InlineData(5, 25, "5-25")]
    public void DayRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().DayRange(start, end);
        string result = builder.Build();

        // Assert
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(1, 32)]
    [InlineData(15, 10)]
    [InlineData(15, 15)]
    public void DayRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.DayRange(start, end));
    }

    [Theory]
    [InlineData(1, "0 0 */1 * *")]
    [InlineData(7, "0 0 */7 * *")]
    [InlineData(31, "0 0 */31 * *")]
    public void EveryNthDay_ValidValues_SetsCorrectExpression(int n, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryNthDay(n);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(-1)]
    public void EveryNthDay_InvalidValues_ThrowsArgumentOutOfRangeException(int n)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.EveryNthDay(n));
    }

    [Fact]
    public void LastDayOfMonth_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().LastDayOfMonth();
        string result = builder.Build();

        // Assert
        Assert.Equal("0 0 L * *", result);
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
        Assert.Contains("* *", result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void InMonth_ValidValues_SetsCorrectMonth(int month)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().InMonth(month);
        string result = builder.Build();

        // Assert
        Assert.Contains($" {month} ", result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void InMonth_InvalidValues_ThrowsArgumentOutOfRangeException(int month)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.InMonth(month));
    }

    [Fact]
    public void InMonths_ValidValues_SetsCommaSeparatedMonths()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().InMonths(1, 6, 12);
        string result = builder.Build();

        // Assert
        Assert.Contains("1,6,12", result);
    }

    [Fact]
    public void InMonths_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.InMonths(1, 13, 6));
    }

    [Theory]
    [InlineData(1, 6, "1-6")]
    [InlineData(3, 9, "3-9")]
    public void MonthRange_ValidRange_SetsCorrectRange(int start, int end, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().MonthRange(start, end);
        string result = builder.Build();

        // Assert
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 13)]
    [InlineData(6, 3)]
    [InlineData(6, 6)]
    public void MonthRange_InvalidRange_ThrowsArgumentException(int start, int end)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.MonthRange(start, end));
    }

    [Theory]
    [InlineData(1, "0 0 1 */1 *")]
    [InlineData(3, "0 0 1 */3 *")]
    [InlineData(12, "0 0 1 */12 *")]
    public void EveryNthMonth_ValidValues_SetsCorrectExpression(int n, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().EveryNthMonth(n);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void EveryNthMonth_InvalidValues_ThrowsArgumentOutOfRangeException(int n)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.EveryNthMonth(n));
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
        Assert.EndsWith("*", result);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, "0")]
    [InlineData(DayOfWeek.Monday, "1")]
    [InlineData(DayOfWeek.Tuesday, "2")]
    [InlineData(DayOfWeek.Wednesday, "3")]
    [InlineData(DayOfWeek.Thursday, "4")]
    [InlineData(DayOfWeek.Friday, "5")]
    [InlineData(DayOfWeek.Saturday, "6")]
    public void OnDayOfWeek_ValidValues_SetsCorrectDayOfWeek(DayOfWeek dayOfWeek, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDayOfWeek(dayOfWeek);
        string result = builder.Build();

        // Assert
        Assert.EndsWith(expected, result);
    }

    [Fact]
    public void OnDaysOfWeek_ValidValues_SetsCommaSeparatedDays()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnDaysOfWeek(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        string result = builder.Build();

        // Assert
        Assert.EndsWith("1,3,5", result);
    }

    [Fact]
    public void Weekdays_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Weekdays();
        string result = builder.Build();

        // Assert
        Assert.Equal("0 0 * * 1-5", result);
    }

    [Fact]
    public void Weekends_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Weekends();
        string result = builder.Build();

        // Assert
        Assert.Equal("0 0 * * 0,6", result);
    }

    [Theory]
    [InlineData(1, DayOfWeek.Monday, "0 0 * * 1#1")]
    [InlineData(2, DayOfWeek.Friday, "0 0 * * 5#2")]
    [InlineData(5, DayOfWeek.Sunday, "0 0 * * 0#5")]
    public void OnNthDayOfWeek_ValidValues_SetsCorrectExpression(int nth, DayOfWeek dayOfWeek, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().OnNthDayOfWeek(nth, dayOfWeek);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, DayOfWeek.Monday)]
    [InlineData(6, DayOfWeek.Friday)]
    [InlineData(-1, DayOfWeek.Sunday)]
    public void OnNthDayOfWeek_InvalidValues_ThrowsArgumentOutOfRangeException(int nth, DayOfWeek dayOfWeek)
    {
        // Arrange
        CronExpressionBuilder builder = new();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.OnNthDayOfWeek(nth, dayOfWeek));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "0 0 * * 1L")]
    [InlineData(DayOfWeek.Friday, "0 0 * * 5L")]
    [InlineData(DayOfWeek.Sunday, "0 0 * * 0L")]
    public void LastDayOfWeek_ValidValues_SetsCorrectExpression(DayOfWeek dayOfWeek, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().LastDayOfWeek(dayOfWeek);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Common Patterns Tests

    [Theory]
    [InlineData(0, 0, "0 0 * * *")]
    [InlineData(12, 30, "30 12 * * *")]
    [InlineData(23, 59, "59 23 * * *")]
    public void Daily_ValidValues_SetsCorrectExpression(int hour, int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Daily(hour, minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 0, 0, "0 0 * * 1")]
    [InlineData(DayOfWeek.Friday, 18, 30, "30 18 * * 5")]
    [InlineData(DayOfWeek.Sunday, 9, 15, "15 9 * * 0")]
    public void Weekly_ValidValues_SetsCorrectExpression(DayOfWeek dayOfWeek, int hour, int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Weekly(dayOfWeek, hour, minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, 0, 0, "0 0 1 * *")]
    [InlineData(15, 12, 30, "30 12 15 * *")]
    [InlineData(31, 23, 59, "59 23 31 * *")]
    public void Monthly_ValidValues_SetsCorrectExpression(int day, int hour, int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Monthly(day, hour, minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, 1, 0, 0, "0 0 1 1 *")]
    [InlineData(12, 25, 18, 30, "30 18 25 12 *")]
    [InlineData(6, 15, 9, 45, "45 9 15 6 *")]
    public void Yearly_ValidValues_SetsCorrectExpression(int month, int day, int hour, int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Yearly(month, day, hour, minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0 * * * *")]
    [InlineData(30, "30 * * * *")]
    [InlineData(59, "59 * * * *")]
    public void Hourly_ValidValues_SetsCorrectExpression(int minute, string expected)
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Hourly(minute);
        string result = builder.Build();

        // Assert
        Assert.Equal(expected, result);
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
        Assert.Equal("* * * * *", result);
    }

    [Fact]
    public void ToString_ReturnsCorrectCronExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().AtMinute(30).AtHour(12);
        string result = builder.ToString();

        // Assert - AtHour sets minute to "0", overriding the previous AtMinute(30)
        Assert.Equal("0 12 * * *", result);
    }

    [Fact]
    public void ImplicitStringConversion_ReturnsCorrectCronExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder().Daily(9, 30);
        string result = builder;

        // Assert
        Assert.Equal("30 9 * * *", result);
    }

    [Fact]
    public void Reset_ResetsAllFieldsToDefault()
    {
        // Arrange
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinute(30)
            .AtHour(12)
            .OnDay(15)
            .InMonth(6)
            .OnDayOfWeek(DayOfWeek.Friday);

        // Act
        builder.Reset();
        string result = builder.Build();

        // Assert
        Assert.Equal("* * * * *", result);
    }

    #endregion

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterface_ChainMultipleMethods_SetsCorrectExpression()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinute(30)
            .AtHour(14)
            .OnDay(15)
            .InMonth(3)
            .OnDayOfWeek(DayOfWeek.Friday);

        string result = builder.Build();

        // Assert - AtHour sets minute to "0", overriding the previous AtMinute(30)
        Assert.Equal("0 14 15 3 5", result);
    }

    [Fact]
    public void FluentInterface_OverwritePreviousValues_UsesLatestValues()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinute(15)
            .AtMinute(30) // This should overwrite the previous value
            .AtHour(9)
            .AtHour(14); // This should overwrite the previous value and set minute to "0"

        string result = builder.Build();

        // Assert - AtHour methods set minute to "0", so final minute will be "0"
        Assert.Equal("0 14 * * *", result);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void ComplexExpression_CombineMultipleFeatures_BuildsCorrectly()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new CronExpressionBuilder()
            .AtMinutes(0, 30)
            .AtHours(9, 12, 18)
            .OnDays(1, 15)
            .InMonths(1, 6, 12)
            .OnDaysOfWeek(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);

        string result = builder.Build();

        // Assert - AtHours overrides the minutes, so it will be "0" not "0,30"
        Assert.Equal("0 9,12,18 1,15 1,6,12 1,3,5", result);
    }

    [Fact]
    public void DefaultValues_BuildWithoutSettingAnyValues_ReturnsAllAsterisks()
    {
        // Arrange & Act
        CronExpressionBuilder builder = new();
        string result = builder.Build();

        // Assert
        Assert.Equal("* * * * *", result);
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
        Assert.IsType<CronExpressionBuilder>(result);
        // EveryHour sets minute to "0", so final result will be "0 * * * *"
        Assert.Equal("0 * * * *", result.Build());
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
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Daily(14, 30); // 2:30 PM daily
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(2025, 9, 3, 14, 30, 0); // Same day 2:30 PM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        
        // Assert
        Assert.Equal(expectedTime, nextOccurrence);
        
        // Test multiple occurrences
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddDays(3)).Take(3).ToList();
        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new(2025, 9, 3, 14, 30, 0), occurrences[0]);
        Assert.Equal(new(2025, 9, 4, 14, 30, 0), occurrences[1]);
        Assert.Equal(new(2025, 9, 5, 14, 30, 0), occurrences[2]);
    }

    [Fact]
    public void Weekly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Every Friday at 6:00 PM
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Weekly(DayOfWeek.Friday, 18, 0);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Wednesday, Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(2025, 9, 5, 18, 0, 0); // Friday, Sep 5, 2025 6:00 PM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        
        // Assert
        Assert.Equal(expectedTime, nextOccurrence);
        Assert.Equal(DayOfWeek.Friday, nextOccurrence.DayOfWeek);
        
        // Test next week
        DateTime nextWeekOccurrence = schedule.GetNextOccurrence(nextOccurrence);
        Assert.Equal(new(2025, 9, 12, 18, 0, 0), nextWeekOccurrence);
        Assert.Equal(DayOfWeek.Friday, nextWeekOccurrence.DayOfWeek);
    }

    [Fact]
    public void Monthly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - 15th of every month at 9:15 AM
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Monthly(15, 9, 15);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(2025, 9, 15, 9, 15, 0); // Sep 15, 2025 9:15 AM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        
        // Assert
        Assert.Equal(expectedTime, nextOccurrence);
        Assert.Equal(15, nextOccurrence.Day);
        
        // Test next month
        DateTime nextMonthOccurrence = schedule.GetNextOccurrence(nextOccurrence);
        Assert.Equal(new(2025, 10, 15, 9, 15, 0), nextMonthOccurrence);
        Assert.Equal(15, nextMonthOccurrence.Day);
    }

    [Fact]
    public void Yearly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - December 25th at 12:00 PM
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Yearly(12, 25, 12, 0);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Sep 3, 2025 10:00 AM
        DateTime expectedTime = new(2025, 12, 25, 12, 0, 0); // Dec 25, 2025 12:00 PM
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        
        // Assert
        Assert.Equal(expectedTime, nextOccurrence);
        Assert.Equal(12, nextOccurrence.Month);
        Assert.Equal(25, nextOccurrence.Day);
        
        // Test next year
        DateTime nextYearOccurrence = schedule.GetNextOccurrence(nextOccurrence);
        Assert.Equal(new(2026, 12, 25, 12, 0, 0), nextYearOccurrence);
    }

    [Fact]
    public void Hourly_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Every hour at 45 minutes
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Hourly(45);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 14, 30, 0); // Sep 3, 2025 2:30 PM
        DateTime expectedTime = new(2025, 9, 3, 14, 45, 0); // Same hour at 45 minutes
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        
        // Assert
        Assert.Equal(expectedTime, nextOccurrence);
        Assert.Equal(45, nextOccurrence.Minute);
        
        // Test next hour
        DateTime nextHourOccurrence = schedule.GetNextOccurrence(nextOccurrence);
        Assert.Equal(new(2025, 9, 3, 15, 45, 0), nextHourOccurrence);
        Assert.Equal(45, nextHourOccurrence.Minute);
    }

    [Fact]
    public void EveryMinutes_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Every 15 minutes
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().EveryMinutes(15);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 14, 7, 0); // Sep 3, 2025 2:07 PM
        DateTime expectedTime = new(2025, 9, 3, 14, 15, 0); // Next 15-minute mark
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        
        // Assert
        Assert.Equal(expectedTime, nextOccurrence);
        
        // Test multiple occurrences
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddHours(2)).Take(5).ToList();
        Assert.Equal(5, occurrences.Count);
        Assert.Equal(new(2025, 9, 3, 14, 15, 0), occurrences[0]);
        Assert.Equal(new(2025, 9, 3, 14, 30, 0), occurrences[1]);
        Assert.Equal(new(2025, 9, 3, 14, 45, 0), occurrences[2]);
        Assert.Equal(new(2025, 9, 3, 15, 0, 0), occurrences[3]);
        Assert.Equal(new(2025, 9, 3, 15, 15, 0), occurrences[4]);
    }

    [Fact]
    public void Weekdays_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Weekdays (Monday-Friday) at midnight
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Weekdays();
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Wednesday, Sep 3, 2025 10:00 AM
        
        // Get next few occurrences
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddDays(10)).Take(5).ToList();
        
        // Assert all are weekdays at midnight
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(occurrence.DayOfWeek != DayOfWeek.Saturday && occurrence.DayOfWeek != DayOfWeek.Sunday);
            Assert.Equal(0, occurrence.Hour);
            Assert.Equal(0, occurrence.Minute);
        }
        
        // Verify specific dates
        Assert.Equal(new(2025, 9, 4, 0, 0, 0), occurrences[0]); // Thursday
        Assert.Equal(new(2025, 9, 5, 0, 0, 0), occurrences[1]); // Friday
        Assert.Equal(new(2025, 9, 8, 0, 0, 0), occurrences[2]); // Monday (skips weekend)
    }

    [Fact]
    public void Weekends_CronExpression_MatchesExpectedTimes()
    {
        // Arrange - Weekends (Saturday & Sunday) at midnight
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().Weekends();
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Wednesday, Sep 3, 2025 10:00 AM
        
        // Get next few occurrences over a longer period to ensure we get weekend days
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddDays(15)).Take(4).ToList();
        
        // Assert all are weekends at midnight
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(occurrence.DayOfWeek == DayOfWeek.Saturday || occurrence.DayOfWeek == DayOfWeek.Sunday);
            Assert.Equal(0, occurrence.Hour);
            Assert.Equal(0, occurrence.Minute);
        }
        
        // Verify we have at least 4 occurrences
        Assert.True(occurrences.Count >= 4);
        
        // Verify we get both Saturday and Sunday
        List<DateTime> saturdays = occurrences.Where(o => o.DayOfWeek == DayOfWeek.Saturday).ToList();
        List<DateTime> sundays = occurrences.Where(o => o.DayOfWeek == DayOfWeek.Sunday).ToList();
        Assert.True(saturdays.Count > 0);
        Assert.True(sundays.Count > 0);
    }

    [Fact]
    public void AtMinutes_MultipleValues_MatchesExpectedTimes()
    {
        // Arrange - At minutes 0, 15, 30, 45 of every hour
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().AtMinutes(0, 15, 30, 45);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 14, 7, 0); // Sep 3, 2025 2:07 PM
        
        // Get next few occurrences
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddHours(2)).Take(6).ToList();
        
        // Assert correct minutes
        Assert.Equal(new(2025, 9, 3, 14, 15, 0), occurrences[0]);
        Assert.Equal(new(2025, 9, 3, 14, 30, 0), occurrences[1]);
        Assert.Equal(new(2025, 9, 3, 14, 45, 0), occurrences[2]);
        Assert.Equal(new(2025, 9, 3, 15, 0, 0), occurrences[3]);
        Assert.Equal(new(2025, 9, 3, 15, 15, 0), occurrences[4]);
        Assert.Equal(new(2025, 9, 3, 15, 30, 0), occurrences[5]);
    }

    [Fact]
    public void AtHours_MultipleValues_MatchesExpectedTimes()
    {
        // Arrange - At hours 9, 12, 18 (9 AM, 12 PM, 6 PM) at minute 0
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().AtHours(9, 12, 18);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Sep 3, 2025 10:00 AM
        
        // Get next few occurrences
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddDays(2)).Take(5).ToList();
        
        // Assert correct hours and minute is always 0
        Assert.Equal(new(2025, 9, 3, 12, 0, 0), occurrences[0]);
        Assert.Equal(new(2025, 9, 3, 18, 0, 0), occurrences[1]);
        Assert.Equal(new(2025, 9, 4, 9, 0, 0), occurrences[2]);
        Assert.Equal(new(2025, 9, 4, 12, 0, 0), occurrences[3]);
        Assert.Equal(new(2025, 9, 4, 18, 0, 0), occurrences[4]);
        
        foreach (DateTime occurrence in occurrences)
        {
            Assert.Equal(0, occurrence.Minute);
            Assert.Contains(occurrence.Hour, new[] { 9, 12, 18 });
        }
    }

    [Fact]
    public void OnDaysOfWeek_MultipleValues_MatchesExpectedTimes()
    {
        // Arrange - Monday, Wednesday, Friday (any time - uses current minute/hour settings)
        CronExpressionBuilder cronExpression = new CronExpressionBuilder().OnDaysOfWeek(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0); // Wednesday, Sep 3, 2025 10:00 AM
        
        // Since the expression is "* * * * 1,3,5", it runs every minute on those days
        // Get just a few occurrences to test the day pattern
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddHours(1)).Take(10).ToList();
        
        // Assert correct days of week
        DayOfWeek[] expectedDays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
        foreach (DateTime occurrence in occurrences)
        {
            Assert.Contains(occurrence.DayOfWeek, expectedDays);
        }
        
        // All occurrences should be on Wednesday since we're only looking at one hour
        Assert.True(occurrences.All(o => o.DayOfWeek == DayOfWeek.Wednesday));
        
        // Should get many occurrences (every minute)
        Assert.Equal(10, occurrences.Count);
    }

    [Fact]
    public void ComplexCronExpression_Integration_MatchesExpectedTimes()
    {
        // Arrange - The complex expression has conflicting settings: AtMinutes vs HourRange vs Weekdays
        // HourRange sets minute to "0", Weekdays sets minute to "0", hour to "0", dayOfWeek to "1-5"
        // So the final result will be: "0 9-17 * * 1-5" (every hour from 9-17 on weekdays at minute 0)
        CronExpressionBuilder cronExpression = new CronExpressionBuilder()
            .AtMinutes(0, 30)  // This will be overridden
            .HourRange(9, 17)  // This sets minute to "0" and hour to "9-17"
            .Weekdays();       // This sets minute to "0", hour to "0", dayOfWeek to "1-5" - overriding hour!
        
        // The final expression should be: "0 0 * * 1-5" (midnight on weekdays only)
        Assert.Equal("0 0 * * 1-5", cronExpression.Build());
        
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 8, 0, 0); // Wednesday, Sep 3, 2025 8:00 AM
        
        // Get occurrences for a few days
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddDays(3)).ToList();
        
        // Should only occur on weekdays at midnight
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(occurrence.DayOfWeek != DayOfWeek.Saturday && occurrence.DayOfWeek != DayOfWeek.Sunday);
            Assert.Equal(0, occurrence.Hour);  // Midnight
            Assert.Equal(0, occurrence.Minute);
        }
        
        // First occurrence should be midnight on the same day (Wednesday)
        Assert.Equal(new(2025, 9, 4, 0, 0, 0), occurrences[0]); // Thursday midnight
    }

    [Fact]
    public void ProperComplexCronExpression_BusinessHours_MatchesExpectedTimes()
    {
        // Arrange - Expression: "0 9,10,11,12,13,14,15,16,17 * * 1,2,3,4,5"
        // AtHours overrides AtMinutes, so final minute is "0"
        CronExpressionBuilder cronExpression = new CronExpressionBuilder()
            .AtMinutes(0, 30)  // This gets overridden by AtHours
            .AtHours(9, 10, 11, 12, 13, 14, 15, 16, 17)  // Sets minute to "0"
            .OnDaysOfWeek(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        
        // The expression should be "0 9,10,11,12,13,14,15,16,17 * * 1,2,3,4,5"
        Assert.Equal("0 9,10,11,12,13,14,15,16,17 * * 1,2,3,4,5", cronExpression.Build());
        
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        
        DateTime baseTime = new(2025, 9, 3, 8, 0, 0); // Wednesday, Sep 3, 2025 8:00 AM
        
        // Get occurrences for one day
        List<DateTime> occurrences = schedule.GetNextOccurrences(baseTime, baseTime.AddDays(1)).Take(20).ToList();
        
        // Should only occur on weekdays, during business hours, at minute 0
        foreach (DateTime occurrence in occurrences)
        {
            Assert.True(occurrence.DayOfWeek != DayOfWeek.Saturday && occurrence.DayOfWeek != DayOfWeek.Sunday);
            Assert.True(occurrence.Hour is >= 9 and <= 17);
            Assert.Equal(0, occurrence.Minute); // Always minute 0 due to AtHours override
        }
        
        // Should have 9 occurrences for Wednesday (9 AM to 5 PM)
        List<DateTime> wednesdayOccurrences = occurrences.Where(o => o.DayOfWeek == DayOfWeek.Wednesday).ToList();
        Assert.Equal(9, wednesdayOccurrences.Count);
    }

    [Fact]
    public void InvalidCronExpression_ThrowsCrontabException()
    {
        // Arrange - Create an invalid cron expression manually
        string invalidCron = "invalid cron expression";
        
        // Act & Assert - NCrontab throws CrontabException, not FormatException
        Assert.Throws<CrontabException>(() => CrontabSchedule.Parse(invalidCron));
    }

    [Theory]
    [InlineData("0 0 * * *")]
    [InlineData("30 14 * * *")]
    [InlineData("0 9 * * 1")]
    [InlineData("0 0 1 * *")]
    [InlineData("0 0 1 1 *")]
    [InlineData("*/15 * * * *")]
    [InlineData("0 */2 * * *")]
    [InlineData("0 9-17 * * 1-5")]
    public void ValidCronExpressions_CanBeParsedByNCrontab(string cronExpression)
    {
        // Act & Assert - Should not throw
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        Assert.NotNull(schedule);
        
        // Verify it can calculate next occurrence
        DateTime baseTime = new(2025, 9, 3, 10, 0, 0);
        DateTime nextOccurrence = schedule.GetNextOccurrence(baseTime);
        Assert.True(nextOccurrence > baseTime);
    }

    #endregion
}
