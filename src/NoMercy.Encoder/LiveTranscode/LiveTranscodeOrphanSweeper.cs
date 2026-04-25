namespace NoMercy.Encoder.LiveTranscode;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;

/// <summary>
/// One-shot hosted service that runs once at startup and deletes any
/// <c>lts-*</c> folders left behind under
/// <see cref="EncoderOptions.ResolvedLiveTranscodeCachePath"/> by a previous
/// server crash or unclean shutdown.
/// </summary>
public class LiveTranscodeOrphanSweeper(
    EncoderOptions options,
    ILogger<LiveTranscodeOrphanSweeper> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        string cacheRoot = options.ResolvedLiveTranscodeCachePath;

        if (!Directory.Exists(cacheRoot))
        {
            logger.LogDebug(
                "LiveTranscodeOrphanSweeper: cache root {Dir} does not exist, nothing to sweep",
                cacheRoot
            );
            return Task.CompletedTask;
        }

        string[] orphans;
        try
        {
            orphans = Directory.GetDirectories(cacheRoot, "lts-*", SearchOption.TopDirectoryOnly);
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

        foreach (string dir in orphans)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                logger.LogInformation(
                    "LiveTranscodeOrphanSweeper: deleted orphan {Dir}",
                    dir
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "LiveTranscodeOrphanSweeper: could not delete orphan {Dir}",
                    dir
                );
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
