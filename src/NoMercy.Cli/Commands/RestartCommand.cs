using System.CommandLine;

namespace NoMercy.Cli.Commands;

internal static class RestartCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("restart")
        {
            Description = "Restart the server"
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            bool ok = await client.PostAsync("/manage/restart", null, ct);

            if (ok)
            {
                Console.WriteLine("Server restart requested.");
                return 0;
            }

            return 1;
        });

        return command;
    }
}
