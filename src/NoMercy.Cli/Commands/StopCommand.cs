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

namespace NoMercy.Cli.Commands;

internal static class StopCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new(name: "stop") { Description = "Stop the server" };

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                bool ok = await client.PostAsync(path: ApiRoutes.Stop, content: null, cancellationToken: ct);

                if (ok)
                {
                    Console.WriteLine(value: "Server is shutting down.");
                    return (int)ExitCode.Success;
                }

                return (int)ExitCode.ServerError;
            }
        );

        return command;
    }
}
