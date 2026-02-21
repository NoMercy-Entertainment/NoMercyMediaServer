using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class EpisodePaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<EpisodePaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(10);
    public string JobName => "Episode ColorPalette Job";

    public EpisodePaletteCronJob(ILogger<EpisodePaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Episode[]> episodes = _context.Episodes
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToList()
            .Chunk(10)
            .ToList();

        if (episodes.Count == 0) return;

        _logger.LogTrace("Found {Count} episode chunks to process", episodes.Count);

        foreach (Episode[] episodeChunk in episodes)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Episode episode in episodeChunk)
            {
                try
                {
                    episode._colorPalette = await MovieDbImageManager.ColorPalette("still", episode.Still);
                }
                catch (Exception)
                {
                    episode._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Episode palette job completed, updated: {Count}", episodes.Sum(x => x.Length));

    }
}
