using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using NoMercy.NmSystem.Information;

namespace NoMercy.Cli.Commands;

internal static class StartCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("start")
        {
            Description = "Start the server"
        };

        Option<bool> devOption = new("--dev")
        {
            Description = "Start the server in development mode"
        };
        command.Options.Add(devOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            bool dev = parseResult.GetValue(devOption);

            if (await IsServerRunning(pipe, ct))
            {
                Console.WriteLine("Server is already running.");
                return 0;
            }

            ProcessStartInfo? startInfo = FindServerStartInfo(dev);

            if (startInfo is null)
            {
                Console.Error.WriteLine("Could not find server executable.");
                return 1;
            }

            try
            {
                Process process = new() { StartInfo = startInfo };
                bool started = process.Start();

                if (started)
                {
                    Console.WriteLine("Server started.");
                    return 0;
                }

                Console.Error.WriteLine("Failed to start server.");
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to start server: {e.Message}");
                return 1;
            }
        });

        return command;
    }

    private static async Task<bool> IsServerRunning(string? pipe, CancellationToken ct)
    {
        try
        {
            using CliClient client = new(pipe);
            Models.StatusResponse? status = await client.GetAsync<Models.StatusResponse>(
                "/manage/status", ct);
            return status is not null;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo? FindServerStartInfo(bool dev)
    {
        return CreateInstalledStartInfo(dev)
               ?? CreateProductionStartInfo(dev)
               ?? CreateDevBinaryStartInfo()
               ?? CreateDotnetRunStartInfo();
    }

    private static ProcessStartInfo? CreateInstalledStartInfo(bool dev)
    {
        string? ownDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location);

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(ownDir, "NoMercyMediaServer" + Info.ExecSuffix);

        if (!File.Exists(candidate))
            return null;

        ProcessStartInfo startInfo = new(candidate)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (dev)
            startInfo.ArgumentList.Add("--dev");

        return startInfo;
    }

    private static ProcessStartInfo? CreateProductionStartInfo(bool dev)
    {
        string exePath = AppFiles.ServerExePath;

        if (!File.Exists(exePath))
            return null;

        ProcessStartInfo startInfo = new(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (dev)
            startInfo.ArgumentList.Add("--dev");

        return startInfo;
    }

    private static ProcessStartInfo? CreateDevBinaryStartInfo()
    {
        string? serverProjectDir = FindProjectDirectory("NoMercy.Service");

        if (serverProjectDir is null)
            return null;

        string execName = "NoMercyMediaServer" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(serverProjectDir, "bin", "Debug",
                $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName),
            Path.Combine(serverProjectDir, "bin", "Release",
                $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName)
        ];

        foreach (string path in searchPaths)
        {
            if (!File.Exists(path)) continue;

            ProcessStartInfo startInfo = new(path)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--dev");
            return startInfo;
        }

        return null;
    }

    private static ProcessStartInfo? CreateDotnetRunStartInfo()
    {
        string? serverProjectDir = FindProjectDirectory("NoMercy.Service");

        if (serverProjectDir is null)
            return null;

        ProcessStartInfo startInfo = new("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(serverProjectDir);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--dev");

        return startInfo;
    }

    private static string? FindProjectDirectory(string projectName)
    {
        string? assemblyLocation = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location);

        string? directory = assemblyLocation;

        while (directory is not null)
        {
            string candidate = Path.Combine(directory, "src", projectName);

            if (Directory.Exists(candidate))
                return candidate;

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
