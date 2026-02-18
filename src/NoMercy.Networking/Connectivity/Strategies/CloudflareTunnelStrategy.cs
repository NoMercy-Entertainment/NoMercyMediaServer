using System.Diagnostics;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Connectivity.Strategies;

public class CloudflareTunnelStrategy : IConnectivityStrategy, IDisposable
{
    private readonly Func<Task>? _checkTunnelAvailability;
    private Process? _tunnelProcess;
    private bool _disposed;

    public string Name => "CloudflareTunnel";
    public int Priority => 3;
    public ConnectivityType Type => ConnectivityType.CloudflareTunnel;

    public CloudflareTunnelStrategy(Func<Task>? checkTunnelAvailability = null)
    {
        _checkTunnelAvailability = checkTunnelAvailability;
    }

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        if (_checkTunnelAvailability is not null)
            await _checkTunnelAvailability();

        if (string.IsNullOrEmpty(Config.CloudflareTunnelToken))
        {
            Logger.Setup("You don't have access to our Cloudflare tunnel service, this is a paid feature.");
            Logger.Setup(
                $"You need to manually forward port {Config.InternalServerPort} to {Config.ExternalServerPort} if you want to use the server outside your local network");
            Logger.Setup(
                "For more information, visit: https://www.noip.com/support/knowledgebase/general-port-forwarding-guide");
            return false;
        }

        try
        {
            _tunnelProcess = new()
            {
                StartInfo = new()
                {
                    FileName = AppFiles.CloudflareDPath,
                    Arguments = $"tunnel run --token {Config.CloudflareTunnelToken}",
                    UseShellExecute = false,
                    WorkingDirectory = AppFiles.BinariesPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            _tunnelProcess.OutputDataReceived += (_, args) =>
                Logger.Setup(args.Data ?? string.Empty, LogEventLevel.Verbose);
            _tunnelProcess.ErrorDataReceived += (_, args) =>
                Logger.Setup(args.Data ?? string.Empty, LogEventLevel.Verbose);
            _tunnelProcess.Exited += (_, args) =>
                Logger.Setup($"Cloudflare tunnel process exited: {args}", LogEventLevel.Warning);

            _tunnelProcess.Start();
            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            Config.NatStatus = NatStatus.Tunneled;
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
        if (_disposed) return;
        StopTunnel();
        _tunnelProcess?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
