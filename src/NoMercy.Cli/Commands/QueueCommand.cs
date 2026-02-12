using System.CommandLine;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class QueueCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command statusCmd = new("status")
        {
            Description = "Show queue statistics"
        };

        statusCmd.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            QueueStatusResponse? queue = await client
                .GetAsync<QueueStatusResponse>("/manage/queue", ct);

            if (queue is null)
            {
                Console.Error.WriteLine("Could not connect to server.");
                return 1;
            }

            Console.WriteLine($"Pending Jobs:  {queue.PendingJobs}");
            Console.WriteLine($"Failed Jobs:   {queue.FailedJobs}");

            if (queue.Workers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"{"Worker",-20} {"Active Threads"}");
                Console.WriteLine(new string('-', 35));
                foreach (KeyValuePair<string, WorkerStatusResponse> w in queue.Workers)
                {
                    Console.WriteLine($"{w.Key,-20} {w.Value.ActiveThreads}");
                }
            }

            return 0;
        });

        Command command = new("queue")
        {
            Description = "Queue management"
        };
        command.Subcommands.Add(statusCmd);

        return command;
    }
}
