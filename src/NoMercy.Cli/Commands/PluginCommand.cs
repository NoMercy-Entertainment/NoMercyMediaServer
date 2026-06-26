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

internal static class PluginCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command listCmd = new("list") { Description = "List installed plugins" };

        listCmd.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using CliClient client = new(pipe);
                List<PluginResponse>? plugins = await client.GetAsync<List<PluginResponse>>(
                    "/manage/plugins",
                    ct
                );

                if (plugins is null)
                {
                    await Console.Error.WriteLineAsync("Could not connect to server.");
                    return 1;
                }

                if (plugins.Count == 0)
                {
                    Console.WriteLine("No plugins installed.");
                    return 0;
                }

                Console.WriteLine($"{"Name", -25} {"Version", -12} {"Status", -10} {"Author"}");
                Console.WriteLine(new string('-', 70));
                foreach (PluginResponse p in plugins)
                {
                    Console.WriteLine($"{p.Name, -25} {p.Version, -12} {p.Status, -10} {p.Author}");
                }

                return 0;
            }
        );

        Command command = new("plugin") { Description = "Manage plugins" };
        command.Subcommands.Add(listCmd);

        return command;
    }
}
