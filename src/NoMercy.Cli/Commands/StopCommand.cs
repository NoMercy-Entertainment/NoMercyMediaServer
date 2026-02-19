using System.CommandLine;

namespace NoMercy.Cli.Commands;

internal static class StopCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("stop")
        {
            Description = "Stop the server"
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            bool ok = await client.PostAsync("/manage/stop", null, ct);

            if (ok)
            {
                Console.WriteLine("Server is shutting down.");
                return 0;
            }

            return 1;
        });

        return command;
    }
}
