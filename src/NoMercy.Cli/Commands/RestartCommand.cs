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
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("restart") { Description = "Restart the server" };

        command.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using CliClient client = new(pipe);
                bool ok = await client.PostAsync(ApiRoutes.Restart, null, ct);

                if (ok)
                {
                    Console.WriteLine("Server restart requested.");
                    return 0;
                }

                return 1;
            }
        );

        return command;
    }
}
