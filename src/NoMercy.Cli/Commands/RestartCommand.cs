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

internal static class RestartCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new(name: "restart") { Description = "Restart the server" };

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                bool ok = await client.PostAsync(path: ApiRoutes.Restart, content: null, cancellationToken: ct);

                if (ok)
                {
                    Console.WriteLine(value: "Server restart requested.");
                    return (int)ExitCode.Success;
                }

                return (int)ExitCode.ServerError;
            }
        );

        return command;
    }
}
