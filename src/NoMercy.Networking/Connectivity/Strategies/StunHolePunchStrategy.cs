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

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Status;
using STUN.Enums;
using STUN.Messages;
using STUN.Messages.StunAttributeValues;

namespace NoMercy.Networking.Connectivity.Strategies;

public class StunHolePunchStrategy : IConnectivityStrategy, IDisposable
{
    private Timer? _keepAliveTimer;
    private UdpClient? _stunSocket;

    public string Name => "StunHolePunch";
    public int Priority => 2;
    public ConnectivityType Type => ConnectivityType.StunHolePunch;

    private readonly IConnectivityStatus _connectivityStatus;

    private readonly ILogger<StunHolePunchStrategy> _logger;

    public StunHolePunchStrategy(
        ILogger<StunHolePunchStrategy> logger,
        IConnectivityStatus connectivityStatus
    )
    {
        _logger = logger;
        _connectivityStatus = connectivityStatus;
    }

    private static readonly (string Host, int Port)[] StunServers =
    [
        ("stun.l.google.com", 19302),
        ("stun.cloudflare.com", 3478),
    ];

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        try
        {
            int localPort = RuntimeServerSettings.Current.StunPort;
            _stunSocket = new(port: localPort);

            // Send STUN binding request to first server
            IPEndPoint? firstResult = await SendStunBindingRequest(
                host: StunServers[0].Host,
                port: StunServers[0].Port,
                ct: ct
            );
            if (firstResult is null)
            {
                _logger.LogDebug(message: "STUN binding request to primary server failed");
                return false;
            }

            // Send STUN binding request to second server from same socket
            IPEndPoint? secondResult = await SendStunBindingRequest(
                host: StunServers[1].Host,
                port: StunServers[1].Port,
                ct: ct
            );

            // Determine NAT type
            if (secondResult is null)
            {
                // Only one server responded, assume restricted cone
                _connectivityStatus.StunPublicIp = firstResult.Address.ToString();
                _connectivityStatus.StunPublicPort = firstResult.Port;
            }
            else if (
                firstResult.Port == secondResult.Port
                && firstResult.Address.Equals(comparand: secondResult.Address)
            )
            {
                // Same public IP:port from different servers → Full Cone or Restricted Cone
                _connectivityStatus.StunPublicIp = firstResult.Address.ToString();
                _connectivityStatus.StunPublicPort = firstResult.Port;
            }
            else if (
                firstResult.Address.Equals(comparand: secondResult.Address)
                && firstResult.Port != secondResult.Port
            )
            {
                // Same IP but different port → Symmetric NAT, hole-punch won't work
                _logger.LogDebug(message: "Symmetric NAT detected — STUN hole-punch not viable");
                Cleanup();
                return false;
            }
            else
            {
                // Different IPs → Symmetric NAT
                _logger.LogDebug(
                    message: "Symmetric NAT detected (different IPs) — STUN hole-punch not viable"
                );
                Cleanup();
                return false;
            }

            _connectivityStatus.NatStatus = NatStatus.HolePunched;
            _logger.LogInformation(
                message: "STUN discovered public endpoint: {StunPublicIp}:{StunPublicPort}", args: [_connectivityStatus.StunPublicIp, _connectivityStatus.StunPublicPort]
            );

            // Start keep-alive to maintain NAT mapping
            _keepAliveTimer = new(
                callback: async _ =>
                {
                    try
                    {
                        if (_stunSocket is null)
                            return;
                        await SendStunBindingRequest(
                            host: StunServers[0].Host,
                            port: StunServers[0].Port,
                            ct: CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(message: "STUN keep-alive failed: {Message}", args: ex.Message);
                    }
                },
                state: null,
                dueTime: TimeSpan.FromSeconds(seconds: 25),
                period: TimeSpan.FromSeconds(seconds: 25)
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(message: "STUN hole-punch failed: {Message}", args: ex.Message);
            Cleanup();
            return false;
        }
    }

    private async Task<IPEndPoint?> SendStunBindingRequest(
        string host,
        int port,
        CancellationToken ct
    )
    {
        if (_stunSocket is null)
            return null;

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostNameOrAddress: host, cancellationToken: ct);
            IPAddress serverAddress =
                addresses.FirstOrDefault(predicate: a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new(message: $"Could not resolve {host}");
            IPEndPoint serverEndpoint = new(address: serverAddress, port: port);

            // Build STUN binding request (RFC 5389)
            StunMessage5389 request = new() { StunMessageType = StunMessageType.BindingRequest };
            byte[] requestBytes = new byte[request.Length];
            request.WriteTo(buffer: requestBytes);

            await _stunSocket.SendAsync(datagram: requestBytes, bytes: requestBytes.Length, endPoint: serverEndpoint);

            // Wait for response with timeout
            using CancellationTokenSource timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            timeoutCts.CancelAfter(millisecondsDelay: 3000);

            UdpReceiveResult result = await _stunSocket.ReceiveAsync(cancellationToken: timeoutCts.Token);

            StunMessage5389 response = new();
            response.TryParse(buffer: result.Buffer);

            // Extract XOR-MAPPED-ADDRESS or MAPPED-ADDRESS from response attributes
            foreach (StunAttribute attr in response.Attributes)
            {
                if (
                    attr.Value is XorMappedAddressStunAttributeValue { Address: not null } xorMapped
                )
                    return new(address: xorMapped.Address, port: xorMapped.Port);

                if (attr.Value is MappedAddressStunAttributeValue { Address: not null } mapped)
                    return new(address: mapped.Address, port: mapped.Port);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                message: "STUN request to {Host}:{Port} failed: {Message}", args: [host, port, ex.Message]
            );
            return null;
        }
    }

    public Task TeardownAsync()
    {
        Cleanup();
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;
        _stunSocket?.Dispose();
        _stunSocket = null;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(obj: this);
    }
}
