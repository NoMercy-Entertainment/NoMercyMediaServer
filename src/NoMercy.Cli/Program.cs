using System.CommandLine;
using NoMercy.Cli.Commands;

namespace NoMercy.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Option<string?> pipeOption = new("--pipe", "-p")
        {
            Description = "Named pipe (Windows) or Unix socket path to connect to"
        };

        RootCommand rootCommand = new("NoMercy MediaServer CLI");
        rootCommand.Options.Add(pipeOption);

        rootCommand.Subcommands.Add(StatusCommand.Create(pipeOption));
        rootCommand.Subcommands.Add(LogsCommand.Create(pipeOption));
        rootCommand.Subcommands.Add(StopCommand.Create(pipeOption));
        rootCommand.Subcommands.Add(RestartCommand.Create(pipeOption));
        rootCommand.Subcommands.Add(ConfigCommand.Create(pipeOption));
        rootCommand.Subcommands.Add(PluginCommand.Create(pipeOption));
        rootCommand.Subcommands.Add(QueueCommand.Create(pipeOption));

        ParseResult parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
