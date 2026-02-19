using CommandLine;
using NoMercy.Service;

namespace NoMercy.Tests.Setup;

public class WindowsServiceHostTests
{
    [Fact]
    public void StartupOptions_RunAsService_DefaultsToFalse()
    {
        StartupOptions options = new();
        Assert.False(options.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_ParsedFromArgs()
    {
        ParserResult<StartupOptions> result = Parser.Default
            .ParseArguments<StartupOptions>(["--service"]);

        StartupOptions? parsed = null;
        result.WithParsed(o => parsed = o);

        Assert.NotNull(parsed);
        Assert.True(parsed.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_FalseWhenNotProvided()
    {
        ParserResult<StartupOptions> result = Parser.Default
            .ParseArguments<StartupOptions>([]);

        StartupOptions? parsed = null;
        result.WithParsed(o => parsed = o);

        Assert.NotNull(parsed);
        Assert.False(parsed.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_CoexistsWithOtherFlags()
    {
        ParserResult<StartupOptions> result = Parser.Default
            .ParseArguments<StartupOptions>(["--service", "--dev"]);

        StartupOptions? parsed = null;
        result.WithParsed(o => parsed = o);

        Assert.NotNull(parsed);
        Assert.True(parsed.RunAsService);
        Assert.True(parsed.Development);
    }

    [Fact]
    public void IsRunningAsService_DefaultsToFalse()
    {
        // Program.IsRunningAsService should default to false
        // since no --service flag was passed during test execution
        Assert.False(Program.IsRunningAsService);
    }
}
