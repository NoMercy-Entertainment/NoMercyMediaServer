using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class EpisodePaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<EpisodePaletteCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().Daily();
    public string JobName => "Episode ColorPalette Job";

    public EpisodePaletteCronJob(ILogger<EpisodePaletteCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        await using MediaContext context = new();

        List<Episode[]> episodes = context.Episodes
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();
        
        _logger.LogTrace("Found {Count} episode chunks to process", episodes.Count);

        foreach (Episode[] episodeChunk in episodes)
        {
            _logger.LogTrace("Processing episode chunk of size: {Size}", episodeChunk.Length);

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
            
            await context.SaveChangesAsync(cancellationToken);
            
        }

        _logger.LogTrace("Episode palette job completed, updated: {Count}", episodes.Sum(x => x.Length));

    }
}