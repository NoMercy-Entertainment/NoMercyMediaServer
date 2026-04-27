using Microsoft.Extensions.Hosting;

namespace NoMercy.Networking.Discovery;

public sealed class MdnsDeviceScannerHostedService(MdnsDeviceScanner scanner) : BackgroundService
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        scanner.Start(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                scanner.Probe();
                await Task.Delay(ProbeInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
