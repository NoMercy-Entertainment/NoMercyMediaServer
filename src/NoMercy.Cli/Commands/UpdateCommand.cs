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
using Newtonsoft.Json;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;

namespace NoMercy.Cli.Commands;

internal static class UpdateCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("update") { Description = "Download and stage a server update" };

        command.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using CliClient client = new(pipe);

                // Step 1: Trigger download
                Console.WriteLine("Downloading update...");
                UpdateResponse? downloadResponse = await client.PostAsync<UpdateResponse>(
                    "/manage/update",
                    null,
                    ct
                );

                if (downloadResponse is null || downloadResponse.Status != "ok")
                {
                    await Console.Error.WriteLineAsync(
                        downloadResponse?.Message ?? "Failed to download update."
                    );
                    return 1;
                }

                Console.WriteLine(downloadResponse.Message);

                // Step 2: Stop the server
                Console.WriteLine("Stopping server...");
                bool stopped = await client.PostAsync("/manage/stop", null, ct);
                if (!stopped)
                {
                    await Console.Error.WriteLineAsync("Failed to send stop command.");
                    return 1;
                }

                // Step 3: Wait for exit
                Console.WriteLine("Waiting for server to exit...");
                await WaitForServerExitAsync(TimeSpan.FromSeconds(30), ct);

                // Step 4: Apply the file swap
                string tempPath = AppFiles.ServerTempExePath;
                string currentPath = AppFiles.ServerExePath;

                if (!File.Exists(tempPath))
                {
                    await Console.Error.WriteLineAsync("No staged update file found.");
                    return 1;
                }

                if (File.Exists(currentPath))
                    File.Delete(currentPath);

                File.Move(tempPath, currentPath);
                await FilePermissions.SetExecutionPermissions(currentPath);

                Console.WriteLine("Update applied. Start the server to use the new version.");
                return 0;
            }
        );

        return command;
    }

    private static async Task WaitForServerExitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private class UpdateResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
}
