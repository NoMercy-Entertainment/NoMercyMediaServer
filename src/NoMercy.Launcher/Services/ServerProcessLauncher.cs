using System.Diagnostics;
using System.Reflection;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;

namespace NoMercy.Launcher.Services;

public class ServerProcessLauncher
{
    private Process? _serverProcess;
    private Process? _appProcess;

    public bool IsServerProcessRunning =>
        _serverProcess is { HasExited: false };

    public bool IsAppProcessRunning =>
        _appProcess is { HasExited: false };

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

    public Task<bool> LaunchAppAsync(string? route = null)
    {
        if (IsAppProcessRunning)
            return Task.FromResult(false);

        ProcessStartInfo? startInfo =
            CreateAppProductionStartInfo()
            ?? CreateAppInstalledStartInfo()
            ?? CreateAppDevBinaryStartInfo()
            ?? CreateAppDotnetRunStartInfo();

        if (startInfo is null)
            return Task.FromResult(false);

        if (!string.IsNullOrEmpty(route))
        {
            startInfo.ArgumentList.Add("--route");
            startInfo.ArgumentList.Add(route);
        }

        _appProcess = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _appProcess.Exited += (_, _) =>
        {
            _appProcess = null;
        };

        bool started = _appProcess.Start();

        if (!started)
        {
            _appProcess = null;
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task<bool> WaitForServerExitAsync(TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(timeout);

        if (_serverProcess is not null)
        {
            try
            {
                await _serverProcess.WaitForExitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        // Server wasn't started by the Tray — poll until it's gone
        while (!cts.Token.IsCancellationRequested)
        {
            if (!IsServerProcessRunning)
                return true;

            try
            {
                await Task.Delay(500, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return !IsServerProcessRunning;
            }
        }

        return !IsServerProcessRunning;
    }

    public async Task ApplyUpdateAsync()
    {
        string tempPath = AppFiles.ServerTempExePath;
        string currentPath = AppFiles.ServerExePath;

        if (!File.Exists(tempPath))
            throw new FileNotFoundException("No staged update found.", tempPath);

        if (File.Exists(currentPath))
            File.Delete(currentPath);

        File.Move(tempPath, currentPath);

        await FilePermissions.SetExecutionPermissions(currentPath);
    }

    public async Task ApplyUpdateIfStagedAsync()
    {
        string tempPath = AppFiles.ServerTempExePath;

        if (File.Exists(tempPath))
            await ApplyUpdateAsync();
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

    private static ProcessStartInfo? CreateAppProductionStartInfo()
    {
        string exePath = AppFiles.AppExePath;

        if (!File.Exists(exePath))
            return null;

        return new(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
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
            UseShellExecute = false,
            CreateNoWindow = true
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
                return new(path) { UseShellExecute = false, CreateNoWindow = true };
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
            UseShellExecute = false,
            CreateNoWindow = true
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
