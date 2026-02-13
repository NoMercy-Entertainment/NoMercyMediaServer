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

    private static string? FindServerProjectDirectory()
    {
        string? assemblyLocation =
            Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

        string? directory = assemblyLocation;

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory, "src", "NoMercy.Server");

            if (Directory.Exists(candidate))
                return candidate;

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
