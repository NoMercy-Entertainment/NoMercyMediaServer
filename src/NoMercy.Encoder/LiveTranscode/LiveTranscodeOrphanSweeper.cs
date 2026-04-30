namespace NoMercy.Encoder.LiveTranscode;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

/// <summary>
/// One-shot hosted service that runs once at startup and deletes any
/// <c>lts-*</c> folders left behind under
/// <see cref="EncoderOptions.ResolvedLiveTranscodeCachePath"/> by a previous
/// server crash or unclean shutdown.
/// </summary>
public class LiveTranscodeOrphanSweeper(
    EncoderOptions options,
    ILogger<LiveTranscodeOrphanSweeper> logger,
    IStorage storage
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        string cacheRoot = options.ResolvedLiveTranscodeCachePath;

        if (!storage.Exists(cacheRoot))
        {
            logger.LogDebug(
                "LiveTranscodeOrphanSweeper: cache root {Dir} does not exist, nothing to sweep",
                cacheRoot
            );
            return Task.CompletedTask;
        }

        IReadOnlyList<StorageEntry> orphans;
        try
        {
            orphans = storage
                .List(cacheRoot, "lts-*", recursive: false)
                .Where(e => e.IsDirectory)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "LiveTranscodeOrphanSweeper: could not enumerate {Dir}",
                cacheRoot
            );
            return Task.CompletedTask;
        }

        foreach (StorageEntry entry in orphans)
        {
            try
            {
                storage.DeleteDirectory(entry.Path, recursive: true);
                logger.LogInformation(
                    "LiveTranscodeOrphanSweeper: deleted orphan {Dir}",
                    entry.Path
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "LiveTranscodeOrphanSweeper: could not delete orphan {Dir}",
                    entry.Path
                );
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
