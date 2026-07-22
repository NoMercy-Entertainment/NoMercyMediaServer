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
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command listCmd = new(name: "list") { Description = "List installed plugins" };

        listCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                List<PluginResponse>? plugins = await client.GetAsync<List<PluginResponse>>(
                    path: ApiRoutes.Plugins,
                    cancellationToken: ct
                );

                if (plugins is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                if (plugins.Count == 0)
                {
                    Console.WriteLine(value: "No plugins installed.");
                    return (int)ExitCode.Success;
                }

                Console.WriteLine(value: $"{"Name", -25} {"Version", -12} {"Status", -10} {"Author"}");
                Console.WriteLine(value: new string(c: '-', count: 70));
                foreach (PluginResponse p in plugins)
                {
                    Console.WriteLine(value: $"{p.Name, -25} {p.Version, -12} {p.Status, -10} {p.Author}");
                }

                return (int)ExitCode.Success;
            }
        );

        Command command = new(name: "plugin") { Description = "Manage plugins" };
        command.Subcommands.Add(item: listCmd);

        return command;
    }
}
