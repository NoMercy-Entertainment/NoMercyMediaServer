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
using NoMercy.Cli.Commands;

namespace NoMercy.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p")
        {
            Description = "Named pipe (Windows) or Unix socket path to connect to",
        };

        RootCommand rootCommand = new(description: "NoMercy MediaServer CLI");
        rootCommand.Options.Add(item: pipeOption);

        ICliClientFactory clientFactory = new CliClientFactory();

        rootCommand.Subcommands.Add(item: StartCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: StatusCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: LogsCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: StopCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: RestartCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: ConfigCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: PluginCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: QueueCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: ResourcesCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: AutoStartCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        rootCommand.Subcommands.Add(item: UpdateCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));

        ParseResult parseResult = rootCommand.Parse(args: args);
        return await parseResult.InvokeAsync();
    }
}
