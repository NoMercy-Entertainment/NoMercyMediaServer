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
        Command command = new("status") { Description = "Show server status" };

        command.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using ICliClient client = clientFactory.Create(pipe);
                StatusResponse? status = await client.GetAsync<StatusResponse>(
                    ApiRoutes.Status,
                    ct
                );

                if (status is null)
                {
                    await Console.Error.WriteLineAsync("Could not connect to server.");
                    return (int)ExitCode.ServerError;
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
                    Console.WriteLine("Mode:         Development");

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
