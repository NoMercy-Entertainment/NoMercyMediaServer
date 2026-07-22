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
using System.Text;
using Newtonsoft.Json;

namespace NoMercy.Cli.Commands;

internal static class AutoStartCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command statusCmd = new(name: "status") { Description = "Check if autostart is enabled" };

        statusCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                AutoStartResponse? response = await client.GetAsync<AutoStartResponse>(
                    path: ApiRoutes.AutoStart,
                    cancellationToken: ct
                );

                if (response is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not retrieve autostart status.");
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(value: $"Autostart:    {(response.Enabled ? "enabled" : "disabled")}");
                return (int)ExitCode.Success;
            }
        );

        Command enableCmd = new(name: "enable") { Description = "Enable autostart" };

        enableCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                return await SetAutoStart(clientFactory: clientFactory, parseResult: parseResult, pipeOption: pipeOption, enabled: true, ct: ct);
            }
        );

        Command disableCmd = new(name: "disable") { Description = "Disable autostart" };

        disableCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                return await SetAutoStart(clientFactory: clientFactory, parseResult: parseResult, pipeOption: pipeOption, enabled: false, ct: ct);
            }
        );

        Command command = new(name: "autostart") { Description = "Manage server autostart" };
        command.Subcommands.Add(item: statusCmd);
        command.Subcommands.Add(item: enableCmd);
        command.Subcommands.Add(item: disableCmd);

        return command;
    }

    private static async Task<int> SetAutoStart(
        ICliClientFactory clientFactory,
        ParseResult parseResult,
        Option<string?> pipeOption,
        bool enabled,
        CancellationToken ct
    )
    {
        string? pipe = parseResult.GetValue(option: pipeOption);
        using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);

        string json = JsonConvert.SerializeObject(value: new { enabled });
        StringContent content = new(content: json, encoding: Encoding.UTF8, mediaType: "application/json");

        bool ok = await client.PostAsync(path: ApiRoutes.AutoStart, content: content, cancellationToken: ct);

        if (ok)
        {
            Console.WriteLine(value: $"Autostart {(enabled ? "enabled" : "disabled")}.");
            return (int)ExitCode.Success;
        }

        return (int)ExitCode.ServerError;
    }

    private class AutoStartResponse
    {
        [JsonProperty(propertyName: "enabled")]
        public bool Enabled { get; set; }
    }
}
