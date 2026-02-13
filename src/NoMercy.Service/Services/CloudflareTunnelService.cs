using System.Diagnostics;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service.Services;

public sealed class CloudflareTunnelService : IHostedService
{
    private Process? _tunnelProcess;
    private readonly string _tunnelToken;
    private readonly string _tunnelName;
    private bool _disposed;

    public CloudflareTunnelService(string tunnelToken, string tunnelName)
    {
        _tunnelToken = tunnelToken;
        _tunnelName = tunnelName;

        // Register application exit handler
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTunnel();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            StopTunnel();
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _tunnelProcess = new()
            {
                StartInfo = new()
                {
                    FileName = AppFiles.CloudflareDPath,
                    Arguments = $"tunnel run --token {_tunnelToken}",
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

            Logger.Setup("Cloudflare tunnel started successfully");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to start Cloudflare tunnel: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
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
                // Try graceful shutdown first
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Shell.ProcessHelper.SendCtrlC(_tunnelProcess);
                else
                    _tunnelProcess.CloseMainWindow();

                // Wait for graceful shutdown
                if (!_tunnelProcess.WaitForExit(3000))
                    _tunnelProcess.Kill(true); // Force kill if graceful shutdown fails
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error stopping Cloudflare tunnel: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        // No finalizer defined for this sealed type, so don't call SuppressFinalize
        // GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Logger.Setup($"Disposing CloudflareTunnelService for {_tunnelName}");
            StopTunnel();
            _tunnelProcess?.Dispose();
        }

        _disposed = true;
    }
}