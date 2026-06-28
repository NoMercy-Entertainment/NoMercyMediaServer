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
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new("update") { Description = "Download and stage a server update" };

        command.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using ICliClient client = clientFactory.Create(pipe);

                // Step 1: Trigger download
                Console.WriteLine("Downloading update...");
                UpdateResponse? downloadResponse = await client.PostAsync<UpdateResponse>(
                    ApiRoutes.Update,
                    null,
                    ct
                );

                if (downloadResponse is null || downloadResponse.Status != "ok")
                {
                    await Console.Error.WriteLineAsync(
                        downloadResponse?.Message ?? "Failed to download update."
                    );
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(downloadResponse.Message);

                // Step 2: Stop the server
                Console.WriteLine("Stopping server...");
                bool stopped = await client.PostAsync(ApiRoutes.Stop, null, ct);
                if (!stopped)
                {
                    await Console.Error.WriteLineAsync("Failed to send stop command.");
                    return (int)ExitCode.ServerError;
                }

                // Step 3: Wait for exit
                Console.WriteLine("Waiting for server to exit...");
                bool exited = await WaitForServerExitAsync(client, TimeSpan.FromSeconds(30), ct);
                if (!exited)
                {
                    await Console.Error.WriteLineAsync(
                        "Warning: the server did not confirm it had stopped within 30s; "
                            + "applying the update anyway."
                    );
                }

                // Step 4: Apply the file swap
                string tempPath = AppFiles.ServerTempExePath;
                string currentPath = AppFiles.ServerExePath;

                if (!File.Exists(tempPath))
                {
                    await Console.Error.WriteLineAsync("No staged update file found.");
                    return (int)ExitCode.ServerError;
                }

                if (File.Exists(currentPath))
                    File.Delete(currentPath);

                File.Move(tempPath, currentPath);
                await FilePermissions.SetExecutionPermissions(currentPath);

                Console.WriteLine("Update applied. Start the server to use the new version.");
                return (int)ExitCode.Success;
            }
        );

        return command;
    }

    private static async Task<bool> WaitForServerExitAsync(
        ICliClient client,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        while (true)
        {
            if (await HasServerStoppedRespondingAsync(client, cts.Token))
            {
                return true;
            }

            try
            {
                await Task.Delay(500, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Re-throw a genuine caller cancellation; a fired timeout simply
                // means the server never confirmed that it stopped.
                ct.ThrowIfCancellationRequested();
                return false;
            }
        }
    }

    // Returns true only when the server's management endpoint is provably
    // unreachable (connection refused / pipe closed), which means the process
    // has exited and the file swap is safe. Any successful or merely
    // unsuccessful HTTP response is treated as "still running" so a transient
    // error can never trigger a premature swap.
    private static async Task<bool> HasServerStoppedRespondingAsync(
        ICliClient client,
        CancellationToken ct
    )
    {
        try
        {
            await client.GetRawAsync(ApiRoutes.Status, ct);
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            return true;
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
