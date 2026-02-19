using NoMercy.Cli.Commands;
using Xunit;

namespace NoMercy.Tests.Cli;

public class FormatHelperTests
{
    [Theory]
    [InlineData(90, "1m 30s")]
    [InlineData(3661, "1h 1m")]
    [InlineData(90061, "1d 1h 1m")]
    [InlineData(30, "0m 30s")]
    [InlineData(0, "0m 0s")]
    [InlineData(86400, "1d 0h 0m")]
    public void FormatUptime_FormatsCorrectly(long totalSeconds, string expected)
    {
        TimeSpan uptime = TimeSpan.FromSeconds(totalSeconds);
        string result = StatusCommand.FormatUptime(uptime);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("serverName", "server_name")]
    [InlineData("server_name", "server_name")]
    [InlineData("server-name", "server_name")]
    [InlineData("queueWorkers", "queue_workers")]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData("ABC", "a_b_c")]
    public void ToSnakeCase_ConvertsCorrectly(string input, string expected)
    {
        string result = ConfigCommand.ToSnakeCase(input);
        Assert.Equal(expected, result);
    }
}
