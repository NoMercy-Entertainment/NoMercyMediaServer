using NoMercy.Encoder.DiscRipping;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;

namespace NoMercy.Service.Workers;

/// <summary>
/// Runs the encoder-layer <see cref="IDriveMonitor"/> polling loop as a hosted
/// background service and bridges each <see cref="DriveEvent"/> into the
/// application event bus so that <see cref="NoMercy.Api.EventHandlers.DriveMonitorEventHandler"/>
/// can forward it to connected SignalR clients on the drives hub.
/// </summary>
public class DriveMonitorWorker(IDriveMonitor driveMonitor, ILogger<DriveMonitorWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DriveMonitorWorker started");

        await foreach (DriveEvent evt in driveMonitor.MonitorAsync(stoppingToken))
        {
            if (!EventBusProvider.IsConfigured)
                continue;

            string methodName = evt.Type switch
            {
                DriveEventType.DriveAdded => "drive_added",
                DriveEventType.DriveRemoved => "drive_removed",
                DriveEventType.DiscInserted => "disc_inserted",
                DriveEventType.DiscEjected => "disc_ejected",
                _ => "drive_changed",
            };

            _ = EventBusProvider.Current.PublishAsync(
                new DriveStateChangedEvent
                {
                    DriveStateData = new
                    {
                        Method = methodName,
                        Drive = evt.Drive.Path,
                        VolumeLabel = evt.Drive.Label,
                        evt.Drive.HasDisc,
                        DiscType = evt.Drive.DiscType.ToString(),
                        Timestamp = DateTime.UtcNow,
                    },
                },
                stoppingToken
            );
        }

        logger.LogInformation("DriveMonitorWorker stopped");
    }
}
