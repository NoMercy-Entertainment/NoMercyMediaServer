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

internal static class ResourcesCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new("resources") { Description = "Show server resource usage" };

        command.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using ICliClient client = clientFactory.Create(pipe);
                ResourcesResponse? resources = await client.GetAsync<ResourcesResponse>(
                    ApiRoutes.Resources,
                    ct
                );

                if (resources is null)
                {
                    await Console.Error.WriteLineAsync("Could not retrieve resource information.");
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(
                    $"CPU:          {resources.Cpu.Total:F1}% (max {resources.Cpu.Max:F1}%)"
                );

                Console.WriteLine(
                    $"Memory:       {resources.Memory.Use:F1} / {resources.Memory.Total:F1} GB ({resources.Memory.Percentage:F1}%)"
                );

                if (resources.Gpu.Count > 0)
                {
                    foreach (GpuInfo gpu in resources.Gpu)
                    {
                        Console.WriteLine(
                            $"GPU {gpu.Index}:        {gpu.Core:F1}% core, {gpu.Memory:F1}% memory, {gpu.Encode:F1}% encode, {gpu.Decode:F1}% decode"
                        );
                    }
                }

                if (resources.Storage.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Storage:");
                    foreach (StorageInfo drive in resources.Storage)
                    {
                        double used = drive.Total - drive.Available;
                        Console.WriteLine(
                            $"  {drive.Name, -12} {used:F1} / {drive.Total:F1} GB ({drive.Percentage:F1}% free)"
                        );
                    }
                }

                return (int)ExitCode.Success;
            }
        );

        return command;
    }
}
