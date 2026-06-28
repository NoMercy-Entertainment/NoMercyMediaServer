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

internal static class QueueCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command statusCmd = new("status") { Description = "Show queue statistics" };

        statusCmd.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using CliClient client = new(pipe);
                QueueStatusResponse? queue = await client.GetAsync<QueueStatusResponse>(
                    ApiRoutes.Queue,
                    ct
                );

                if (queue is null)
                {
                    await Console.Error.WriteLineAsync("Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine($"Pending Jobs:  {queue.PendingJobs}");
                Console.WriteLine($"Failed Jobs:   {queue.FailedJobs}");

                if (queue.Workers.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{"Worker", -20} {"Active Threads"}");
                    Console.WriteLine(new string('-', 35));
                    foreach (KeyValuePair<string, WorkerStatusResponse> w in queue.Workers)
                    {
                        Console.WriteLine($"{w.Key, -20} {w.Value.ActiveThreads}");
                    }
                }

                return (int)ExitCode.Success;
            }
        );

        Command command = new("queue") { Description = "Queue management" };
        command.Subcommands.Add(statusCmd);

        return command;
    }
}
