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
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

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

    public CloudflareTunnelStrategy(
        IConnectivityStatus connectivityStatus,
        Func<Task>? checkTunnelAvailability = null
    )
    {
        _checkTunnelAvailability = checkTunnelAvailability;
        _connectivityStatus = connectivityStatus;
    }

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        if (_checkTunnelAvailability is not null)
            await _checkTunnelAvailability();

        if (string.IsNullOrEmpty(_connectivityStatus.CloudflareTunnelToken))
        {
            Logger.Setup(
                "You don't have access to our Cloudflare tunnel service, this is a paid feature."
            );
            Logger.Setup(
                $"You need to manually forward port {RuntimeServerSettings.Current.InternalServerPort} to {RuntimeServerSettings.Current.ExternalServerPort} if you want to use the server outside your local network"
            );
            Logger.Setup(
                "For more information, visit: https://www.noip.com/support/knowledgebase/general-port-forwarding-guide"
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
                    Environment =
                    {
                        ["TUNNEL_TOKEN"] = _connectivityStatus.CloudflareTunnelToken,
                    },
                    UseShellExecute = false,
                    WorkingDirectory = AppFiles.DependenciesPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            _tunnelProcess.OutputDataReceived += (_, args) =>
                Logger.Setup(args.Data.OrEmpty(), LogEventLevel.Verbose);
            _tunnelProcess.ErrorDataReceived += (_, args) =>
                Logger.Setup(args.Data.OrEmpty(), LogEventLevel.Verbose);
            _tunnelProcess.Exited += (_, args) =>
                Logger.Setup($"Cloudflare tunnel process exited: {args}", LogEventLevel.Warning);

            _tunnelProcess.Start();
            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            _connectivityStatus.NatStatus = NatStatus.Tunneled;
            Logger.Setup("Cloudflare tunnel started successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to start Cloudflare tunnel: {ex.Message}");
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Shell.ProcessHelper.SendCtrlC(_tunnelProcess);
                else
                    _tunnelProcess.CloseMainWindow();

                if (!_tunnelProcess.WaitForExit(3000))
                    _tunnelProcess.Kill(true);
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error stopping Cloudflare tunnel: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        StopTunnel();
        _tunnelProcess?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
