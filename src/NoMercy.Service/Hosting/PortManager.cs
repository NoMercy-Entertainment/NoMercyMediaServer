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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Configuration;

namespace NoMercy.Service.Hosting;

public class PortManager : IPortManager
{
    private readonly ILogger<PortManager> _logger;
    private readonly ICertificateService _certificateService;

    public PortManager(ILogger<PortManager> logger, ICertificateService certificateService)
    {
        _logger = logger;
        _certificateService = certificateService;
    }

    public async Task EnsurePortAvailable(int port)
    {
        if (IsPortAvailable(port: port))
            return;

        _logger.LogInformation(message: "Port {Port} is in use — checking for stale instances...", args: port);
        string processInfo = await FindProcessOnPortAsync(port: port);

        if (!string.IsNullOrEmpty(value: processInfo))
            _logger.LogInformation(
                message: "Process holding port {Port}:\n{ProcessInfo}", args: [port, processInfo]
            );

        int blockingPid = ParsePidFromPortInfo(processInfo: processInfo);

        if (blockingPid <= 0)
        {
            if (_certificateService.HasValidCertificate())
            {
                _logger.LogError(
                    message: "Port {Port} is in use by an unknown process. NoMercy is registered on this port and cannot use a different one. Free the port and restart.",
                    args: port
                );
            }
            else
            {
                _logger.LogError(
                    message: "Port {Port} is in use but cannot identify the process. Please free it manually.",
                    args: port
                );
            }

            throw new StartupAbortException(
                message: $"Port {port} is in use and the blocking process could not be identified."
            );
        }

        bool isStaleInstance = false;
        string blockingProcessName = "unknown";
        try
        {
            Process blockingProcess = Process.GetProcessById(processId: blockingPid);
            blockingProcessName = blockingProcess.ProcessName;
            isStaleInstance = blockingProcessName == "NoMercyMediaServer";
        }
        catch
        {
            // Process may have exited between detection and lookup
        }

        if (isStaleInstance)
        {
            _logger.LogInformation(
                message: "Stale NoMercyMediaServer instance detected (PID {BlockingPid}). Auto-killing...",
                args: blockingPid
            );
        }
        else
        {
            bool isRegistered = _certificateService.HasValidCertificate();

            if (isRegistered)
            {
                _logger.LogError(
                    message: "Port {Port} is in use by {BlockingProcessName} (PID {BlockingPid}). NoMercy is registered on this port and cannot use a different one. Free the port and restart.", args: [port, blockingProcessName, blockingPid]
                );
                throw new StartupAbortException(
                    message: $"Port {port} is in use by {blockingProcessName} (PID {blockingPid}) and NoMercy is registered on this port."
                );
            }

            int alternativePort = FindNextAvailablePort(startPort: port + 1);
            _logger.LogInformation(
                message: "Port {Port} is in use by {BlockingProcessName} (PID {BlockingPid}). Server is not yet registered — using port {AlternativePort} instead.", args: [port, blockingProcessName, blockingPid, alternativePort]
            );
            RuntimeServerSettings.Current.InternalServerPort = alternativePort;
            return;
        }

        bool portFreed = await KillAndWaitAsync(pid: blockingPid, port: port);

        if (!portFreed)
        {
            throw new StartupAbortException(
                message: $"Port {port} could not be freed after killing the stale process (PID {blockingPid})."
            );
        }

        _logger.LogInformation(message: "Port freed — continuing startup...");
    }

    public int FindNextAvailablePort(int startPort)
    {
        const int MaxPort = 65535;

        for (int candidate = startPort; candidate <= MaxPort; candidate++)
        {
            if (IsPortAvailable(port: candidate))
                return candidate;
        }

        _logger.LogError(
            message: "No available port found in range {StartPort}–{MaxPort}.", args: [startPort, MaxPort]
        );
        throw new StartupAbortException(message: $"No available port found in range {startPort}-{MaxPort}.");
    }

    public bool IsPortAvailable(int port)
    {
        try
        {
            using TcpListener listener = new(localaddr: IPAddress.Any, port: port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(exception: ex, message: "Error checking port availability for {Port}", args: port);
            return false;
        }
    }

    public async Task<bool> HandlePortInUse(int port, IOException ex)
    {
        if (
            ex.InnerException is not SocketException socketEx
            || socketEx.SocketErrorCode != SocketError.AddressAlreadyInUse
        )
        {
            return false;
        }

        _logger.LogWarning(
            message: "Host failed to bind to port {Port} (Address already in use). Attempting recovery...",
            args: port
        );
        await EnsurePortAvailable(port: port);

        // If EnsurePortAvailable didn't exit the process, we can retry on the same port
        // (if it was freed) or we already switched RuntimeServerSettings.Current.InternalServerPort.
        return true;
    }

    private async Task<bool> KillAndWaitAsync(int pid, int port)
    {
        try
        {
            Process process = Process.GetProcessById(processId: pid);
            process.Kill();

            _logger.LogInformation(
                message: "Sent kill signal to PID {Pid}. Waiting for port {Port} to be freed...", args: [pid, port]
            );

            // Wait up to 5 seconds for the port to be freed
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(millisecondsDelay: 100);
                if (IsPortAvailable(port: port))
                    return true;
            }

            _logger.LogError(
                message: "Timed out waiting for port {Port} to be freed by PID {Pid}.", args: [port, pid]
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                exception: ex,
                message: "Failed to kill process {Pid} or wait for port {Port}.", args: [pid, port]
            );
            return false;
        }
    }

    private async Task<string> FindProcessOnPortAsync(int port)
    {
        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            // Windows: netstat -ano | findstr :<port>
            try
            {
                using Process process = new();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments =
                    $"/c \"netstat -ano | findstr LISTENING | findstr :{port}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                return await process.StandardOutput.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(exception: ex, message: "Failed to run netstat on Windows");
                return string.Empty;
            }
        }

        if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
        )
        {
            // Linux/macOS: lsof -i :<port>
            try
            {
                using Process process = new();
                process.StartInfo.FileName = "lsof";
                process.StartInfo.Arguments = $"-i :{port}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                return await process.StandardOutput.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(exception: ex, message: "Failed to run lsof on Linux/macOS");
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private int ParsePidFromPortInfo(string processInfo)
    {
        if (string.IsNullOrWhiteSpace(value: processInfo))
            return -1;

        return RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows)
            ? ParsePidFromNetstat(processInfo: processInfo)
            : ParsePidFromLsof(processInfo: processInfo);
    }

    // netstat -ano output: the last whitespace-delimited column of a LISTENING
    // row is the owning PID.
    // Example: TCP    0.0.0.0:7625    0.0.0.0:0    LISTENING    1234
    internal static int ParsePidFromNetstat(string processInfo)
    {
        if (string.IsNullOrWhiteSpace(value: processInfo))
            return -1;

        string[] lines = processInfo.Split(
            separator: new[] { '\r', '\n' },
            options: StringSplitOptions.RemoveEmptyEntries
        );
        if (lines.Length > 0)
        {
            string[] parts = lines[0].Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(s: parts[^1], result: out int pid))
                return pid;
        }

        return -1;
    }

    // lsof -i output: the second column of the first data row (after the
    // header line) is the owning PID.
    internal static int ParsePidFromLsof(string processInfo)
    {
        if (string.IsNullOrWhiteSpace(value: processInfo))
            return -1;

        string[] lines = processInfo.Split(separator: '\n');
        if (lines.Length > 1)
        {
            string[] parts = lines[1].Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && int.TryParse(s: parts[1], result: out int pid))
                return pid;
        }

        return -1;
    }
}
