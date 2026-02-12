using System.CommandLine;
using Xunit;

namespace NoMercy.Tests.Cli;

public class CommandStructureTests
{
    private readonly RootCommand _root;

    public CommandStructureTests()
    {
        _root = new RootCommand("NoMercy MediaServer CLI");

        Option<string?> pipeOption = new("--pipe", "-p");
        _root.Options.Add(pipeOption);

        _root.Subcommands.Add(NoMercy.Cli.Commands.StatusCommand.Create(pipeOption));
        _root.Subcommands.Add(NoMercy.Cli.Commands.LogsCommand.Create(pipeOption));
        _root.Subcommands.Add(NoMercy.Cli.Commands.StopCommand.Create(pipeOption));
        _root.Subcommands.Add(NoMercy.Cli.Commands.RestartCommand.Create(pipeOption));
        _root.Subcommands.Add(NoMercy.Cli.Commands.ConfigCommand.Create(pipeOption));
        _root.Subcommands.Add(NoMercy.Cli.Commands.PluginCommand.Create(pipeOption));
        _root.Subcommands.Add(NoMercy.Cli.Commands.QueueCommand.Create(pipeOption));
    }

    [Fact]
    public void RootCommand_HasAllExpectedSubcommands()
    {
        List<string> names = _root.Subcommands.Select(c => c.Name).ToList();

        Assert.Contains("status", names);
        Assert.Contains("logs", names);
        Assert.Contains("stop", names);
        Assert.Contains("restart", names);
        Assert.Contains("config", names);
        Assert.Contains("plugin", names);
        Assert.Contains("queue", names);
        Assert.Equal(7, names.Count);
    }

    [Fact]
    public void StatusCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse("status");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesTailOption()
    {
        ParseResult result = _root.Parse("logs --tail 50");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesFollowOption()
    {
        ParseResult result = _root.Parse("logs --follow");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesShortAliases()
    {
        ParseResult result = _root.Parse("logs -n 20 -f");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesLevelFilter()
    {
        ParseResult result = _root.Parse("logs --level Error,Warning");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesTypeFilter()
    {
        ParseResult result = _root.Parse("logs --type App");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void StopCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse("stop");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RestartCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse("restart");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ConfigGetCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse("config get");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ConfigSetCommand_ParsesKeyAndValue()
    {
        ParseResult result = _root.Parse("config set server_name MyServer");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void PluginListCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse("plugin list");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void QueueStatusCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse("queue status");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GlobalPipeOption_ParsesOnAnyCommand()
    {
        ParseResult result = _root.Parse("--pipe /tmp/test.sock status");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GlobalPipeOption_ParsesShortAlias()
    {
        ParseResult result = _root.Parse("-p MyPipe status");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void InvalidCommand_ProducesError()
    {
        ParseResult result = _root.Parse("nonexistent");
        Assert.NotEmpty(result.Errors);
    }
}
