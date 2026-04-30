using System.Diagnostics;
using System.Reflection;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

namespace NoMercy.NmSystem;

public class AppProcessManager
{
    private readonly object _lock = new();
    private readonly IStorageBackend _backend;
    private Process? _appProcess;

    public AppProcessManager(IStorageBackend? backend = null)
    {
        _backend = backend ?? new SystemIoStorageBackend();
    }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _appProcess is { HasExited: false };
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_lock)
            {
                return _appProcess is { HasExited: false } ? _appProcess.Id : null;
            }
        }
    }

    public bool Start(string? route = null)
    {
        lock (_lock)
        {
            if (_appProcess is { HasExited: false })
                return false;

            ProcessStartInfo? startInfo =
                CreateProductionStartInfo(_backend)
                ?? CreateInstalledStartInfo(_backend)
                ?? CreateDevBinaryStartInfo(_backend)
                ?? CreateDotnetRunStartInfo(_backend);

            if (startInfo is not null && !string.IsNullOrEmpty(route))
            {
                startInfo.ArgumentList.Add("--route");
                startInfo.ArgumentList.Add(route);
            }

            if (startInfo is null)
                return false;

            _appProcess = new() { StartInfo = startInfo, EnableRaisingEvents = true };

            _appProcess.Exited += (_, _) =>
            {
                lock (_lock)
                {
                    _appProcess = null;
                }
            };

            bool started = _appProcess.Start();

            if (!started)
            {
                _appProcess = null;
                return false;
            }

            Shell.ChildProcessManager.Attach(_appProcess);

            return true;
        }
    }

    public bool Stop()
    {
        lock (_lock)
        {
            if (_appProcess is null or { HasExited: true })
            {
                _appProcess = null;
                return false;
            }

            try
            {
                _appProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }

            _appProcess = null;
            return true;
        }
    }

    private static ProcessStartInfo? CreateProductionStartInfo(IStorageBackend backend)
    {
        string exePath = AppFiles.AppExePath;

        if (!backend.FileExists(exePath))
            return null;

        return new(exePath) { UseShellExecute = false, CreateNoWindow = true };
    }

    private static ProcessStartInfo? CreateInstalledStartInfo(IStorageBackend backend)
    {
        string? ownDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (ownDir is null)
            return null;

        string candidate = Path.Combine(ownDir, "NoMercyApp" + Info.ExecSuffix);

        if (!backend.FileExists(candidate))
            return null;

        return new(candidate) { UseShellExecute = false, CreateNoWindow = true };
    }

    private static ProcessStartInfo? CreateDevBinaryStartInfo(IStorageBackend backend)
    {
        string? appProjectDir = FindProjectDirectory("NoMercy.App", backend);

        if (appProjectDir is null)
            return null;

        string execName = "NoMercyApp" + Info.ExecSuffix;

        string[] searchPaths =
        [
            Path.Combine(
                appProjectDir,
                "bin",
                "Debug",
                $"net{Environment.Version.Major}.{Environment.Version.Minor}",
                execName
            ),
            Path.Combine(
                appProjectDir,
                "bin",
                "Release",
                $"net{Environment.Version.Major}.{Environment.Version.Minor}",
                execName
            ),
        ];

        foreach (string path in searchPaths)
        {
            if (backend.FileExists(path))
                return new(path) { UseShellExecute = false, CreateNoWindow = true };
        }

        return null;
    }

    private static ProcessStartInfo? CreateDotnetRunStartInfo(IStorageBackend backend)
    {
        string? appProjectDir = FindProjectDirectory("NoMercy.App", backend);

        if (appProjectDir is null)
            return null;

        ProcessStartInfo startInfo = new("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(appProjectDir);

        return startInfo;
    }

    private static string? FindProjectDirectory(string projectName, IStorageBackend backend)
    {
        string? assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        string? directory = assemblyLocation;

        while (directory is not null)
        {
            string candidate = Path.Combine(directory, "src", projectName);

            if (backend.DirectoryExists(candidate))
                return candidate;

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
