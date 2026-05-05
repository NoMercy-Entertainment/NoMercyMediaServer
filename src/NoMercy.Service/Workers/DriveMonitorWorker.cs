using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.OpticalMedia.Drives;

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

        // BackgroundServiceExceptionBehavior defaults to StopHost since .NET 6
        // — letting MonitorAsync throw would tear the whole server down. Wrap
        // the loop and back off on transient failures (USB hub flapping,
        // permission flips). A real fatal error logs and exits cleanly.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (
                    DriveEvent evt in driveMonitor
                        .MonitorAsync(stoppingToken)
                        .WithCancellation(stoppingToken)
                )
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
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DriveMonitorWorker iteration failed; retrying in 5s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("DriveMonitorWorker stopped");
    }
}
