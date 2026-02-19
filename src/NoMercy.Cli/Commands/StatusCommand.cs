using System.CommandLine;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class StatusCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("status")
        {
            Description = "Show server status"
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            StatusResponse? status = await client.GetAsync<StatusResponse>(
                "/manage/status", ct);

            if (status is null)
            {
                Console.Error.WriteLine("Could not connect to server.");
                return 1;
            }

            TimeSpan uptime = TimeSpan.FromSeconds(status.UptimeSeconds);

            Console.WriteLine($"Status:       {status.Status}");
            Console.WriteLine($"Server:       {status.ServerName}");
            Console.WriteLine($"Version:      {status.Version}");
            Console.WriteLine($"Platform:     {status.Platform} ({status.Architecture})");
            Console.WriteLine($"OS:           {status.Os}");
            Console.WriteLine($"Uptime:       {FormatUptime(uptime)}");
            Console.WriteLine($"Started:      {status.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            if (status.IsDev)
                Console.WriteLine($"Mode:         Development");

            return 0;
        });

        return command;
    }

    internal static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
    }
}
