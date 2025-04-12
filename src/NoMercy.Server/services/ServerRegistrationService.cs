using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Server.services;

public class ServerRegistrationService : IHostedService
{
    private readonly CloudflareTunnelService _tunnelService;

    public ServerRegistrationService()
    {
        if (string.IsNullOrEmpty(Config.CloudflareTunnelToken))
        {
            Logger.Setup("Cloudflare tunnel token is not set, skipping registration.");
            return;
        }
        
        _tunnelService = new(
            Config.CloudflareTunnelToken,
            $"nomercy-mediaserver-{Info.DeviceId}"
        );
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Config.UseCloudflareProxy)
        {
            await _tunnelService.StartAsync(cancellationToken);
        }
        else
        {
            Logger.Setup("Cloudflare proxy is disabled, skipping registration.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _tunnelService.StopAsync(cancellationToken);
    }
}