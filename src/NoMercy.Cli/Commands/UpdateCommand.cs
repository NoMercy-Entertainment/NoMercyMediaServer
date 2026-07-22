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
        Command command = new(name: "update") { Description = "Download and stage a server update" };

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);

                // Step 1: Trigger download
                Console.WriteLine(value: "Downloading update...");
                UpdateResponse? downloadResponse = await client.PostAsync<UpdateResponse>(
                    path: ApiRoutes.Update,
                    content: null,
                    cancellationToken: ct
                );

                if (downloadResponse is null || downloadResponse.Status != "ok")
                {
                    await Console.Error.WriteLineAsync(
                        value: downloadResponse?.Message ?? "Failed to download update."
                    );
                    return (int)ExitCode.ServerError;
                }

                Console.WriteLine(value: downloadResponse.Message);

                // Step 2: Stop the server
                Console.WriteLine(value: "Stopping server...");
                bool stopped = await client.PostAsync(path: ApiRoutes.Stop, content: null, cancellationToken: ct);
                if (!stopped)
                {
                    await Console.Error.WriteLineAsync(value: "Failed to send stop command.");
                    return (int)ExitCode.ServerError;
                }

                // Step 3: Wait for exit
                Console.WriteLine(value: "Waiting for server to exit...");
                bool exited = await WaitForServerExitAsync(client: client, timeout: TimeSpan.FromSeconds(seconds: 30), ct: ct);
                if (!exited)
                {
                    await Console.Error.WriteLineAsync(
                        value: "Warning: the server did not confirm it had stopped within 30s; "
                               + "applying the update anyway."
                    );
                }

                // Step 4: Apply the file swap
                string tempPath = AppFiles.ServerTempExePath;
                string currentPath = AppFiles.ServerExePath;

                if (!File.Exists(path: tempPath))
                {
                    await Console.Error.WriteLineAsync(value: "No staged update file found.");
                    return (int)ExitCode.ServerError;
                }

                if (File.Exists(path: currentPath))
                    File.Delete(path: currentPath);

                File.Move(sourceFileName: tempPath, destFileName: currentPath);
                await FilePermissions.SetExecutionPermissions(path: currentPath);

                Console.WriteLine(value: "Update applied. Start the server to use the new version.");
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
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
        cts.CancelAfter(delay: timeout);

        while (true)
        {
            if (await HasServerStoppedRespondingAsync(client: client, ct: cts.Token))
            {
                return true;
            }

            try
            {
                await Task.Delay(millisecondsDelay: 500, cancellationToken: cts.Token);
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
            await client.GetRawAsync(path: ApiRoutes.Status, cancellationToken: ct);
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
        [JsonProperty(propertyName: "status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty(propertyName: "message")]
        public string Message { get; set; } = string.Empty;
    }
}
