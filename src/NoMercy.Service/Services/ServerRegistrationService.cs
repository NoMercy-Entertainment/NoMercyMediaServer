using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Service.Services;

public class ServerRegistrationService : IHostedService, IDisposable
{
    private CloudflareTunnelService? _tunnelService { get; set; }
    private Task? _executingTask;
    private readonly CancellationTokenSource _stoppingCts = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executingTask = ExecuteAsync(_stoppingCts.Token);
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!Config.Started && !cancellationToken.IsCancellationRequested)
                await Task.Delay(1000, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (Config.NatStatus == NatStatus.Open)
            {
                Logger.Setup("NAT status is open, you can access your server from outside your local network.");
                return;
            }

            // Test if the server is reachable from the outside
            Config.PortForwarded = await Networking.Networking.IsPortOpenAsync();
            if (Config.PortForwarded)
            {
                Logger.Setup(
                    "Your server is port forwarded, you can access your server from outside your local network.");
                return;
            }

            await Register.GetTunnelAvailability();

            // Cloudflare token is not available
            if (string.IsNullOrEmpty(Config.CloudflareTunnelToken))
            {
                Logger.Setup("You don't have access to our Cloudflare tunnel service, this is a paid feature.");

                Logger.Setup(
                    $"You need to manually forward port {Config.InternalServerPort} to {Config.ExternalServerPort} if you want to use the server outside your local network");
                Logger.Setup(
                    "For more information, visit: https://www.noip.com/support/knowledgebase/general-port-forwarding-guide");
                return;
            }

            _tunnelService = new(
                Config.CloudflareTunnelToken,
                $"nomercy-mediaserver-{Info.DeviceId}"
            );

            switch (Config.NatStatus)
            {
                // Api provided a cloudflare tunnel token and NAT status is unknown, closed or none
                case NatStatus.Unknown:
                    Logger.Setup("NAT status is unknown, registering with Cloudflare proxy.");
                    await _tunnelService.StartAsync(cancellationToken);
                    return;
                case NatStatus.Closed:
                    Logger.Setup("NAT status is closed, registering with Cloudflare proxy.");
                    await _tunnelService.StartAsync(cancellationToken);
                    return;
                case NatStatus.None:
                    Logger.Setup("NAT status is unavailable, registering with Cloudflare proxy.");
                    await _tunnelService.StartAsync(cancellationToken);
                    return;
                default:
                    Logger.Setup("Cloudflare proxy is disabled, skipping registration.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error in ServerRegistrationService: {ex.Message}", LogEventLevel.Error);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;

        try
        {
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            // Wait for the executing task to complete with a timeout
            await Task.WhenAny(_executingTask, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
        }

        if (_tunnelService != null)
        {
            await _tunnelService.StopAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        _stoppingCts.Cancel();
        _stoppingCts.Dispose();
        GC.SuppressFinalize(this);
    }
}