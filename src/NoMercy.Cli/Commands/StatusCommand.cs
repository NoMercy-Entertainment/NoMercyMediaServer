// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.CommandLine;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class StatusCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new(name: "status") { Description = "Show server status" };

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                StatusResponse? status = await client.GetAsync<StatusResponse>(
                    path: ApiRoutes.Status,
                    cancellationToken: ct
                );

                if (status is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                TimeSpan uptime = TimeSpan.FromSeconds(seconds: status.UptimeSeconds);

                Console.WriteLine(value: $"Status:       {status.Status}");
                Console.WriteLine(value: $"Server:       {status.ServerName}");
                Console.WriteLine(value: $"Version:      {status.Version}");
                Console.WriteLine(value: $"Platform:     {status.Platform} ({status.Architecture})");
                Console.WriteLine(value: $"OS:           {status.Os}");
                Console.WriteLine(value: $"Uptime:       {FormatUptime(uptime: uptime)}");
                Console.WriteLine(value: $"Started:      {status.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
                if (status.IsDev)
                    Console.WriteLine(value: "Mode:         Development");

                return (int)ExitCode.Success;
            }
        );

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
