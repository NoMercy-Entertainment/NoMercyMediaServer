using System.Net;
using System.Net.Sockets;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using STUN.Client;
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

    private static readonly (string Host, int Port)[] StunServers =
    [
        ("stun.l.google.com", 19302),
        ("stun.cloudflare.com", 3478)
    ];

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        try
        {
            int localPort = Config.StunPort;
            _stunSocket = new(localPort);

            // Send STUN binding request to first server
            IPEndPoint? firstResult = await SendStunBindingRequest(StunServers[0].Host, StunServers[0].Port, ct);
            if (firstResult is null)
            {
                Logger.Setup("STUN binding request to primary server failed", LogEventLevel.Debug);
                return false;
            }

            // Send STUN binding request to second server from same socket
            IPEndPoint? secondResult = await SendStunBindingRequest(StunServers[1].Host, StunServers[1].Port, ct);

            // Determine NAT type
            if (secondResult is null)
            {
                // Only one server responded, assume restricted cone
                Config.StunPublicIp = firstResult.Address.ToString();
                Config.StunPublicPort = firstResult.Port;
            }
            else if (firstResult.Port == secondResult.Port && firstResult.Address.Equals(secondResult.Address))
            {
                // Same public IP:port from different servers → Full Cone or Restricted Cone
                Config.StunPublicIp = firstResult.Address.ToString();
                Config.StunPublicPort = firstResult.Port;
            }
            else if (firstResult.Address.Equals(secondResult.Address) && firstResult.Port != secondResult.Port)
            {
                // Same IP but different port → Symmetric NAT, hole-punch won't work
                Logger.Setup("Symmetric NAT detected — STUN hole-punch not viable", LogEventLevel.Debug);
                Cleanup();
                return false;
            }
            else
            {
                // Different IPs → Symmetric NAT
                Logger.Setup("Symmetric NAT detected (different IPs) — STUN hole-punch not viable", LogEventLevel.Debug);
                Cleanup();
                return false;
            }

            Config.NatStatus = NatStatus.HolePunched;
            Logger.Setup($"STUN discovered public endpoint: {Config.StunPublicIp}:{Config.StunPublicPort}");

            // Start keep-alive to maintain NAT mapping
            _keepAliveTimer = new(async _ =>
            {
                try
                {
                    if (_stunSocket is null) return;
                    await SendStunBindingRequest(StunServers[0].Host, StunServers[0].Port, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.Setup($"STUN keep-alive failed: {ex.Message}", LogEventLevel.Debug);
                }
            }, null, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25));

            return true;
        }
        catch (Exception ex)
        {
            Logger.Setup($"STUN hole-punch failed: {ex.Message}", LogEventLevel.Debug);
            Cleanup();
            return false;
        }
    }

    private async Task<IPEndPoint?> SendStunBindingRequest(string host, int port, CancellationToken ct)
    {
        if (_stunSocket is null) return null;

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, ct);
            IPAddress serverAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new($"Could not resolve {host}");
            IPEndPoint serverEndpoint = new(serverAddress, port);

            // Build STUN binding request (RFC 5389)
            StunMessage5389 request = new();
            request.StunMessageType = StunMessageType.BindingRequest;
            byte[] requestBytes = new byte[request.Length];
            request.WriteTo(requestBytes);

            await _stunSocket.SendAsync(requestBytes, requestBytes.Length, serverEndpoint);

            // Wait for response with timeout
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(3000);

            UdpReceiveResult result = await _stunSocket.ReceiveAsync(timeoutCts.Token);

            StunMessage5389 response = new();
            response.TryParse(result.Buffer);

            // Extract XOR-MAPPED-ADDRESS or MAPPED-ADDRESS from response attributes
            foreach (StunAttribute attr in response.Attributes)
            {
                if (attr.Value is XorMappedAddressStunAttributeValue { Address: not null } xorMapped)
                    return new(xorMapped.Address, xorMapped.Port);

                if (attr.Value is MappedAddressStunAttributeValue { Address: not null } mapped)
                    return new(mapped.Address, mapped.Port);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Setup($"STUN request to {host}:{port} failed: {ex.Message}", LogEventLevel.Debug);
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
        GC.SuppressFinalize(this);
    }
}
