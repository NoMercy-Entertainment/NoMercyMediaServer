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
using System.Globalization;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class ResourcesCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new(name: "resources") { Description = "Show server resource usage" };

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
                ResourcesResponse? resources = await client.GetAsync<ResourcesResponse>(
                    path: ApiRoutes.Resources,
                    cancellationToken: ct
                );

                if (resources is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not retrieve resource information.");
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(
                    value: $"CPU:          {F1(value: resources.Cpu.Total)}% (max {F1(value: resources.Cpu.Max)}%)"
                );

                Console.WriteLine(
                    value: $"Memory:       {F1(value: resources.Memory.Use)} / {F1(value: resources.Memory.Total)} GB ({F1(value: resources.Memory.Percentage)}%)"
                );

                if (resources.Gpu.Count > 0)
                {
                    foreach (GpuInfo gpu in resources.Gpu)
                    {
                        Console.WriteLine(
                            value: $"GPU {gpu.Index}:        {F1(value: gpu.Core)}% core, {F1(value: gpu.Memory)}% memory, {F1(value: gpu.Encode)}% encode, {F1(value: gpu.Decode)}% decode"
                        );
                    }
                }

                if (resources.Storage.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(value: "Storage:");
                    foreach (StorageInfo drive in resources.Storage)
                    {
                        double used = drive.Total - drive.Available;
                        Console.WriteLine(
                            value: $"  {drive.Name, -12} {F1(value: used)} / {F1(value: drive.Total)} GB ({F1(value: drive.Percentage)}% free)"
                        );
                    }
                }

                return (int)ExitCode.Success;
            }
        );

        return command;
    }

    // Bare ":F1" in an interpolated string formats with the current thread's
    // culture, so on any non-US-decimal locale (e.g. nl-NL) the CLI would print
    // "12,5%" instead of "12.5%" — inconsistent with the docs/scripts that
    // assume a dot, and a regression trap for a self-hosted tool with a global
    // audience. Force invariant formatting explicitly.
    private static string F1(double value) => value.ToString(format: "F1", provider: CultureInfo.InvariantCulture);
}
