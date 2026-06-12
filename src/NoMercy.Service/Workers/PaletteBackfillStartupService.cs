using NoMercy.Database;
using NoMercy.MediaProcessing.Images.Palettes;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercyQueue;

namespace NoMercy.Service.Workers;

/// <summary>
/// On boot, dispatches a single <see cref="PaletteBackfillJob"/> onto the
/// low-priority palette queue if the one-shot backfill has not yet completed.
/// The job is self-continuing and self-terminating — it re-enqueues itself
/// until all tables are drained, then sets the completion flag and stops.
/// </summary>
public class PaletteBackfillStartupService(ILogger<PaletteBackfillStartupService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AppDbContext db = new();
            bool complete = await PaletteBackfillState.IsCompleteAsync(db, cancellationToken);
            if (complete)
            {
                logger.LogDebug("Palette backfill already complete — skipping dispatch");
                return;
            }

            QueueRunner.Current?.Dispatcher.Dispatch(new PaletteBackfillJob(), "palette", 20);
            logger.LogInformation("Palette backfill job dispatched on startup");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dispatch palette backfill on startup; continuing");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
