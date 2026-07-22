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
using System.Diagnostics;
using System.Reflection;
using NoMercy.Cli.Models;
using NoMercy.NmSystem.Information;

namespace NoMercy.Cli.Commands;

internal static class StartCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new(name: "start") { Description = "Start the server" };

        Option<bool> devOption = new(name: "--dev")
        {
            Description = "Start the server in development mode",
        };
        command.Options.Add(item: devOption);

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                bool dev = parseResult.GetValue(option: devOption);

                if (await IsServerRunning(clientFactory: clientFactory, pipe: pipe, ct: ct))
                {
                    Console.WriteLine(value: "Server is already running.");
                    return (int)ExitCode.Success;
                }

                ProcessStartInfo? startInfo = FindServerStartInfo(dev: dev);

                if (startInfo is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not find server executable.");
                    return (int)ExitCode.ConfigurationError;
                }

                try
                {
                    Process process = new() { StartInfo = startInfo };
                    bool started = process.Start();

                    if (started)
                    {
                        Console.WriteLine(value: "Server started.");
                        return (int)ExitCode.Success;
                    }

                    await Console.Error.WriteLineAsync(value: "Failed to start server.");
                    return (int)ExitCode.ServerError;
                }
                catch (Exception e)
                {
                    await Console.Error.WriteLineAsync(value: $"Failed to start server: {e.Message}");
                    return (int)ExitCode.ServerError;
                }
            }
        );

        return command;
    }

    private static async Task<bool> IsServerRunning(
        ICliClientFactory clientFactory,
        string? pipe,
        CancellationToken ct
    )
    {
        try
        {
            using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);
            StatusResponse? status = await client.GetAsync<StatusResponse>(path: ApiRoutes.Status, cancellationToken: ct);
            return status is not null;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo? FindServerStartInfo(bool dev)
    {
        return CreateInstalledStartInfo(dev: dev)
            ?? CreateProductionStartInfo(dev: dev)
            ?? CreateDevBinaryStartInfo()
            ?? CreateDotnetRunStartInfo();
    }

    private static ProcessStartInfo? CreateInstalledStartInfo(bool dev)
    {
        string? ownDir = Path.GetDirectoryName(
            path: Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(path1: ownDir, path2: "NoMercyMediaServer" + Info.ExecSuffix);

        if (!File.Exists(path: candidate))
            return null;

        ProcessStartInfo startInfo = new(fileName: candidate)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (dev)
            startInfo.ArgumentList.Add(item: "--dev");

        return startInfo;
    }

    private static ProcessStartInfo? CreateProductionStartInfo(bool dev)
    {
        string exePath = AppFiles.ServerExePath;

        if (!File.Exists(path: exePath))
            return null;

        ProcessStartInfo startInfo = new(fileName: exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (dev)
            startInfo.ArgumentList.Add(item: "--dev");

        return startInfo;
    }

    private static ProcessStartInfo? CreateDevBinaryStartInfo()
    {
        string? serverProjectDir = FindProjectDirectory(projectName: "NoMercy.Service");

        if (serverProjectDir is null)
            return null;

        string execName = "NoMercyMediaServer" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(paths: [serverProjectDir, "bin", "Debug", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName]
            ),
            Path.Combine(paths: [serverProjectDir, "bin", "Release", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName]
            ),
        ];

        foreach (string path in searchPaths)
        {
            if (!File.Exists(path: path))
                continue;

            ProcessStartInfo startInfo = new(fileName: path)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add(item: "--dev");
            return startInfo;
        }

        return null;
    }

    private static ProcessStartInfo? CreateDotnetRunStartInfo()
    {
        string? serverProjectDir = FindProjectDirectory(projectName: "NoMercy.Service");

        if (serverProjectDir is null)
            return null;

        ProcessStartInfo startInfo = new(fileName: "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(item: "run");
        startInfo.ArgumentList.Add(item: "--project");
        startInfo.ArgumentList.Add(item: serverProjectDir);
        startInfo.ArgumentList.Add(item: "--");
        startInfo.ArgumentList.Add(item: "--dev");

        return startInfo;
    }

    private static string? FindProjectDirectory(string projectName)
    {
        string? assemblyLocation = Path.GetDirectoryName(path: Assembly.GetExecutingAssembly().Location);

        string? directory = assemblyLocation;

        while (directory is not null)
        {
            string candidate = Path.Combine(path1: directory, path2: "src", path3: projectName);

            if (Directory.Exists(path: candidate))
                return candidate;

            directory = Path.GetDirectoryName(path: directory);
        }

        return null;
    }
}
