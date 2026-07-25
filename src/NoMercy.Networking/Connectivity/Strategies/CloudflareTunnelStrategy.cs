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
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Networking.Connectivity.Strategies;

public partial class CloudflareTunnelStrategy : IConnectivityStrategy, IDisposable
{
    private readonly Func<Task>? _checkTunnelAvailability;
    private readonly IConnectivityStatus _connectivityStatus;
    private Process? _tunnelProcess;
    private bool _disposed;

    private static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// cloudflared logs a line per edge connection it registers. That line is the first
    /// moment the tunnel can actually carry traffic, so it is what "the tunnel is up" has to
    /// mean. Both the current and the older wording are matched so a cloudflared upgrade
    /// cannot silently turn every tunnel into a timeout.
    /// </summary>
    [GeneratedRegex(
        @"registered tunnel connection|connection .* registered",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ConnectionRegisteredPattern();

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

    public async Task<ConnectivityResult> TryEstablishAsync(CancellationToken ct)
    {
        if (_checkTunnelAvailability is not null)
            await _checkTunnelAvailability();

        if (string.IsNullOrEmpty(_connectivityStatus.CloudflareTunnelToken))
        {
            _logger.LogInformation(
                "You don't have access to our Cloudflare tunnel service, this is a paid feature."
            );
            _logger.LogInformation(
                "You need to manually forward port {InternalServerPort} to {ExternalServerPort} if you want to use the server outside your local network",
                [
                    RuntimeServerSettings.Current.InternalServerPort,
                    RuntimeServerSettings.Current.ExternalServerPort,
                ]
            );
            _logger.LogInformation(
                "For more information, visit: https://www.noip.com/support/knowledgebase/general-port-forwarding-guide"
            );
            return ConnectivityResult.Failed();
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
                    Environment = { ["TUNNEL_TOKEN"] = _connectivityStatus.CloudflareTunnelToken },
                    UseShellExecute = false,
                    WorkingDirectory = AppFiles.DependenciesPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            // A started process is not a working tunnel. cloudflared exits non-fatally on a
            // revoked token, a clock skew or no egress, and reporting success on Start()
            // left the server advertising a tunnel address nothing was listening on.
            TaskCompletionSource<bool> registered = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            void Watch(string? line)
            {
                if (string.IsNullOrEmpty(line))
                    return;

                _logger.LogTrace(line);

                if (ConnectionRegisteredPattern().IsMatch(line))
                    registered.TrySetResult(true);
            }

            _tunnelProcess.OutputDataReceived += (_, args) => Watch(args.Data);
            _tunnelProcess.ErrorDataReceived += (_, args) => Watch(args.Data);
            _tunnelProcess.Exited += (_, _) =>
            {
                _logger.LogWarning("Cloudflare tunnel process exited");
                registered.TrySetResult(false);
            };

            _tunnelProcess.Start();
            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
                ct
            );
            timeout.CancelAfter(RegistrationTimeout);

            bool connected;
            try
            {
                connected = await registered.Task.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                connected = false;
                _logger.LogWarning(
                    "Cloudflare tunnel did not register a connection within {Seconds}s",
                    RegistrationTimeout.TotalSeconds
                );
            }

            if (!connected)
            {
                StopTunnel();
                return ConnectivityResult.Failed();
            }

            _connectivityStatus.NatStatus = NatStatus.Tunneled;
            _logger.LogInformation("Cloudflare tunnel registered a connection");
            return ConnectivityResult.Verified();
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Failed to start Cloudflare tunnel: {Message}", ex.Message);
            StopTunnel();
            return ConnectivityResult.Failed();
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
            _logger.LogInformation("Error stopping Cloudflare tunnel: {Message}", ex.Message);
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
