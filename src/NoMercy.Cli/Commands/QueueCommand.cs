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
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command statusCmd = new(name: "status") { Description = "Show queue statistics" };

        statusCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                QueueStatusResponse? queue = await client.GetAsync<QueueStatusResponse>(
                    path: ApiRoutes.Queue,
                    cancellationToken: ct
                );

                if (queue is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(value: $"Pending Jobs:  {queue.PendingJobs}");
                Console.WriteLine(value: $"Failed Jobs:   {queue.FailedJobs}");

                if (queue.Workers.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(value: $"{"Worker", -20} {"Active Threads"}");
                    Console.WriteLine(value: new string(c: '-', count: 35));
                    foreach (KeyValuePair<string, WorkerStatusResponse> w in queue.Workers)
                    {
                        Console.WriteLine(value: $"{w.Key, -20} {w.Value.ActiveThreads}");
                    }
                }

                return (int)ExitCode.Success;
            }
        );

        Command command = new(name: "queue") { Description = "Queue management" };
        command.Subcommands.Add(item: statusCmd);

        return command;
    }
}
