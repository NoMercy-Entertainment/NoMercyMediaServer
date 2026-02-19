using Xunit;
using NoMercy.Tray.Services;

namespace NoMercy.Tests.Service;

public class TrayIconManagerTests
{
    [Fact]
    public void FormatUptime_Seconds_ReturnsMinutesAndSeconds()
    {
        string result = TrayIconManager.FormatUptime(45);
        Assert.Equal("0m 45s", result);
    }

    [Fact]
    public void FormatUptime_Minutes_ReturnsMinutesAndSeconds()
    {
        string result = TrayIconManager.FormatUptime(125);
        Assert.Equal("2m 5s", result);
    }

    [Fact]
    public void FormatUptime_Hours_ReturnsHoursAndMinutes()
    {
        string result = TrayIconManager.FormatUptime(3725);
        Assert.Equal("1h 2m", result);
    }

    [Fact]
    public void FormatUptime_Days_ReturnsDaysHoursMinutes()
    {
        long totalSeconds = 2 * 86400 + 5 * 3600 + 30 * 60;
        string result = TrayIconManager.FormatUptime(totalSeconds);
        Assert.Equal("2d 5h 30m", result);
    }

    [Fact]
    public void FormatUptime_Zero_ReturnsZeroMinutesZeroSeconds()
    {
        string result = TrayIconManager.FormatUptime(0);
        Assert.Equal("0m 0s", result);
    }

    [Fact]
    public void FormatUptime_ExactlyOneHour_ReturnsHoursFormat()
    {
        string result = TrayIconManager.FormatUptime(3600);
        Assert.Equal("1h 0m", result);
    }

    [Fact]
    public void FormatUptime_ExactlyOneDay_ReturnsDaysFormat()
    {
        string result = TrayIconManager.FormatUptime(86400);
        Assert.Equal("1d 0h 0m", result);
    }
}
