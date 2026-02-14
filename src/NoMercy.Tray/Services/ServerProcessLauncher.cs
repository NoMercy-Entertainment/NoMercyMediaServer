using System.Diagnostics;
using System.Reflection;
using NoMercy.NmSystem.Information;

namespace NoMercy.Tray.Services;

public class ServerProcessLauncher
{
    private Process? _serverProcess;

    public bool IsServerProcessRunning =>
        _serverProcess is { HasExited: false };

    public Process? ServerProcess => _serverProcess;

    public Task<bool> StartServerAsync()
    {
        if (IsServerProcessRunning)
            return Task.FromResult(false);

        ProcessStartInfo? startInfo =
            CreateProductionStartInfo()
            ?? CreateInstalledStartInfo()
            ?? CreateDevBinaryStartInfo()
            ?? CreateDotnetRunStartInfo();

        if (startInfo is null)
            return Task.FromResult(false);

        _serverProcess = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _serverProcess.Exited += (_, _) =>
        {
            _serverProcess = null;
        };

        bool started = _serverProcess.Start();
        return Task.FromResult(started);
    }

    private static ProcessStartInfo? CreateProductionStartInfo()
    {
        string exePath = AppFiles.ServerExePath;

        if (!File.Exists(exePath))
            return null;

        return new(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static ProcessStartInfo? CreateInstalledStartInfo()
    {
        string? ownDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location);

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(ownDir, "NoMercyMediaServer" + Info.ExecSuffix);

        if (!File.Exists(candidate))
            return null;

        return new(candidate)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static ProcessStartInfo? CreateDevBinaryStartInfo()
    {
        string? serverBinary = FindServerBinary();

        if (serverBinary is null)
            return null;

        ProcessStartInfo startInfo = new(serverBinary)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--dev");

        return startInfo;
    }

    private static ProcessStartInfo? CreateDotnetRunStartInfo()
    {
        string? serverProjectDir = FindServerProjectDirectory();

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

    private static string? FindServerBinary()
    {
        string? serverProjectDir = FindServerProjectDirectory();

        if (serverProjectDir is null)
            return null;

        string execName = "NoMercyMediaServer" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(serverProjectDir, "bin", "Debug", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName),
            Path.Combine(serverProjectDir, "bin", "Release", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName)
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public Task<bool> LaunchAppAsync()
    {
        ProcessStartInfo? startInfo =
            CreateAppProductionStartInfo()
            ?? CreateAppInstalledStartInfo()
            ?? CreateAppDevBinaryStartInfo()
            ?? CreateAppDotnetRunStartInfo();

        if (startInfo is null)
            return Task.FromResult(false);

        Process process = new() { StartInfo = startInfo };
        bool started = process.Start();
        return Task.FromResult(started);
    }

    private static ProcessStartInfo? CreateAppProductionStartInfo()
    {
        string exePath = AppFiles.AppExePath;

        if (!File.Exists(exePath))
            return null;

        return new(exePath)
        {
            UseShellExecute = false
        };
    }

    private static ProcessStartInfo? CreateAppInstalledStartInfo()
    {
        string? ownDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location);

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(ownDir, "NoMercyApp" + Info.ExecSuffix);

        if (!File.Exists(candidate))
            return null;

        return new(candidate)
        {
            UseShellExecute = false
        };
    }

    private static ProcessStartInfo? CreateAppDevBinaryStartInfo()
    {
        string? appProjectDir = FindProjectDirectory("NoMercy.App");

        if (appProjectDir is null)
            return null;

        string execName = "NoMercyApp" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(appProjectDir, "bin", "Debug", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName),
            Path.Combine(appProjectDir, "bin", "Release", $"net{Environment.Version.Major}.{Environment.Version.Minor}", execName)
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
                return new(path) { UseShellExecute = false };
        }

        return null;
    }

    private static ProcessStartInfo? CreateAppDotnetRunStartInfo()
    {
        string? appProjectDir = FindProjectDirectory("NoMercy.App");

        if (appProjectDir is null)
            return null;

        ProcessStartInfo startInfo = new("dotnet")
        {
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(appProjectDir);

        return startInfo;
    }

    private static string? FindServerProjectDirectory()
    {
        return FindProjectDirectory("NoMercy.Service");
    }

    private static string? FindProjectDirectory(string projectName)
    {
        string? assemblyLocation =
            Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

        string? directory = assemblyLocation;

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory, "src", projectName);

            if (Directory.Exists(candidate))
                return candidate;

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
