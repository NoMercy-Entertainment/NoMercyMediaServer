using System.CommandLine;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class LogsCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Option<int> tailOption = new("--tail", "-n")
        {
            Description = "Number of log entries to show",
            DefaultValueFactory = _ => 100
        };

        Option<bool> followOption = new("--follow", "-f")
        {
            Description = "Continuously poll for new logs",
            DefaultValueFactory = _ => false
        };

        Option<string?> levelOption = new("--level")
        {
            Description = "Filter by log level (e.g. Information,Warning,Error)"
        };

        Option<string?> typeOption = new("--type")
        {
            Description = "Filter by log type"
        };

        Command command = new("logs")
        {
            Description = "View server logs"
        };
        command.Options.Add(tailOption);
        command.Options.Add(followOption);
        command.Options.Add(levelOption);
        command.Options.Add(typeOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            int tail = parseResult.GetValue(tailOption);
            bool follow = parseResult.GetValue(followOption);
            string? level = parseResult.GetValue(levelOption);
            string? type = parseResult.GetValue(typeOption);

            using CliClient client = new(pipe);

            string query = BuildQuery(tail, level, type);
            DateTime lastTime = DateTime.MinValue;

            do
            {
                List<LogEntryResponse>? logs = await client
                    .GetAsync<List<LogEntryResponse>>($"/manage/logs{query}", ct);

                if (logs is null)
                {
                    Console.Error.WriteLine("Could not connect to server.");
                    return 1;
                }

                foreach (LogEntryResponse entry in logs)
                {
                    if (entry.Time <= lastTime) continue;
                    lastTime = entry.Time;

                    Console.WriteLine(
                        $"{entry.Time:HH:mm:ss} [{entry.Level,-12}] [{entry.Type}] {entry.Message}");
                }

                if (follow)
                    await Task.Delay(2000, ct);
            }
            while (follow && !ct.IsCancellationRequested);

            return 0;
        });

        return command;
    }

    internal static string BuildQuery(int tail, string? level, string? type)
    {
        List<string> parts = [$"tail={tail}"];
        if (!string.IsNullOrWhiteSpace(level))
            parts.Add($"levels={Uri.EscapeDataString(level)}");
        if (!string.IsNullOrWhiteSpace(type))
            parts.Add($"types={Uri.EscapeDataString(type)}");
        return "?" + string.Join("&", parts);
    }
}
