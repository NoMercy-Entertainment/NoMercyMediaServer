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
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Networking.Connectivity.Strategies;

public class CloudflareTunnelStrategy : IConnectivityStrategy, IDisposable
{
    private readonly Func<Task>? _checkTunnelAvailability;
    private readonly IConnectivityStatus _connectivityStatus;
    private Process? _tunnelProcess;
    private bool _disposed;

    public string Name => "CloudflareTunnel";
    public int Priority => 3;
    public ConnectivityType Type => ConnectivityType.CloudflareTunnel;

    private readonly ILogger<CloudflareTunnelStrategy> _logger;

    public CloudflareTunnelStrategy(
        ILogger<CloudflareTunnelStrategy> logger,
        IConnectivityStatus connectivityStatus,
        Func<Task>? checkTunnelAvailability = null
    )
    {
        _logger = logger;
        _checkTunnelAvailability = checkTunnelAvailability;
        _connectivityStatus = connectivityStatus;
    }

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        if (_checkTunnelAvailability is not null)
            await _checkTunnelAvailability();

        if (string.IsNullOrEmpty(value: _connectivityStatus.CloudflareTunnelToken))
        {
            _logger.LogInformation(
                message: "You don't have access to our Cloudflare tunnel service, this is a paid feature."
            );
            _logger.LogInformation(
                message: "You need to manually forward port {InternalServerPort} to {ExternalServerPort} if you want to use the server outside your local network", args: [RuntimeServerSettings.Current.InternalServerPort, RuntimeServerSettings.Current.ExternalServerPort]
            );
            _logger.LogInformation(
                message: "For more information, visit: https://www.noip.com/support/knowledgebase/general-port-forwarding-guide"
            );
            return false;
        }

        try
        {
            _tunnelProcess = new()
            {
                StartInfo = new()
                {
                    FileName = AppFiles.CloudflareDPath,
                    Arguments = "tunnel run",
                    // Pass the tunnel token via environment variable rather than the
                    // command line so it is not exposed in /proc/<pid>/cmdline or via
                    // WMI Win32_Process.CommandLine. cloudflared reads TUNNEL_TOKEN.
                    Environment = { [key: "TUNNEL_TOKEN"] = _connectivityStatus.CloudflareTunnelToken },
                    UseShellExecute = false,
                    WorkingDirectory = AppFiles.DependenciesPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            _tunnelProcess.OutputDataReceived += (_, args) => _logger.LogTrace(message: args.Data.OrEmpty());
            _tunnelProcess.ErrorDataReceived += (_, args) => _logger.LogTrace(message: args.Data.OrEmpty());
            _tunnelProcess.Exited += (_, args) =>
                _logger.LogWarning(message: "Cloudflare tunnel process exited: {Args}", args: args);

            _tunnelProcess.Start();
            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            _connectivityStatus.NatStatus = NatStatus.Tunneled;
            _logger.LogInformation(message: "Cloudflare tunnel started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(message: "Failed to start Cloudflare tunnel: {Message}", args: ex.Message);
            return false;
        }
    }

    public Task TeardownAsync()
    {
        StopTunnel();
        return Task.CompletedTask;
    }

    private void StopTunnel()
    {
        try
        {
            if (_tunnelProcess is { HasExited: false })
            {
                if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
                    Shell.ProcessHelper.SendCtrlC(process: _tunnelProcess);
                else
                    _tunnelProcess.CloseMainWindow();

                if (!_tunnelProcess.WaitForExit(milliseconds: 3000))
                    _tunnelProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(message: "Error stopping Cloudflare tunnel: {Message}", args: ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        StopTunnel();
        _tunnelProcess?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(obj: this);
    }
}
