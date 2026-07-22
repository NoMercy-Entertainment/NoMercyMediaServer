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
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class ConfigCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command getCmd = new(name: "get") { Description = "Show current configuration" };

        getCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                ConfigResponse? config = await client.GetAsync<ConfigResponse>(
                    path: ApiRoutes.Config,
                    cancellationToken: ct
                );

                if (config is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(value: $"Server Name:      {config.ServerName}");
                Console.WriteLine(value: $"Internal Port:    {config.InternalPort}");
                Console.WriteLine(value: $"External Port:    {config.ExternalPort}");
                Console.WriteLine(value: $"Queue Workers:    {config.QueueWorkers}");
                Console.WriteLine(value: $"Encoder Workers:  {config.EncoderWorkers}");
                Console.WriteLine(value: $"Cron Workers:     {config.CronWorkers}");
                Console.WriteLine(value: $"Data Workers:     {config.DataWorkers}");
                Console.WriteLine(value: $"Image Workers:    {config.ImageWorkers}");
                Console.WriteLine(value: $"File Workers:     {config.FileWorkers}");
                Console.WriteLine(value: $"Request Workers:  {config.RequestWorkers}");
                Console.WriteLine(value: $"Swagger:          {config.Swagger}");
                return (int)ExitCode.Success;
            }
        );

        Argument<string> keyArg = new(name: "key") { Description = "Configuration key to set" };
        Argument<string> valArg = new(name: "value") { Description = "Value to set" };

        Command setCmd = new(name: "set") { Description = "Update a configuration value" };
        setCmd.Arguments.Add(item: keyArg);
        setCmd.Arguments.Add(item: valArg);

        setCmd.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                string key = parseResult.GetValue(argument: keyArg)!;
                string val = parseResult.GetValue(argument: valArg)!;

                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);

                Dictionary<string, object> payload = new()
                {
                    { ToSnakeCase(input: key), ParseValue(val: val) },
                };
                string json = JsonConvert.SerializeObject(value: payload);
                StringContent content = new(content: json, encoding: Encoding.UTF8, mediaType: "application/json");

                bool ok = await client.PutAsync(path: ApiRoutes.Config, content: content, cancellationToken: ct);

                if (ok)
                {
                    Console.WriteLine(value: $"Configuration updated: {key} = {val}");
                    return (int)ExitCode.Success;
                }

                return (int)ExitCode.ServerError;
            }
        );

        Command command = new(name: "config") { Description = "Manage server configuration" };
        command.Subcommands.Add(item: getCmd);
        command.Subcommands.Add(item: setCmd);

        return command;
    }

    internal static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(value: input))
            return input;

        StringBuilder sb = new();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[index: i];
            if (c == '-' || c == '_')
            {
                sb.Append(value: '_');
                continue;
            }
            if (char.IsUpper(c: c) && i > 0 && input[index: i - 1] != '_' && input[index: i - 1] != '-')
                sb.Append(value: '_');
            sb.Append(value: char.ToLowerInvariant(c: c));
        }
        return sb.ToString();
    }

    private static object ParseValue(string val)
    {
        if (int.TryParse(s: val, result: out int intVal))
            return intVal;
        if (bool.TryParse(value: val, result: out bool boolVal))
            return boolVal;
        return val;
    }
}
