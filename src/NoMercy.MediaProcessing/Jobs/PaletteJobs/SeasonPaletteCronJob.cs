using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class SeasonPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<SeasonPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(30);
    public string JobName => "Season ColorPalette Job";

    public SeasonPaletteCronJob(ILogger<SeasonPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Season[]> seasons = _context.Seasons
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(100)
            .ToList()
            .Chunk(10)
            .ToList();

        if (seasons.Count == 0) return;

        _logger.LogTrace("Found {Count} season chunks to process", seasons.Count);

        foreach (Season[] seasonChunk in seasons)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Season season in seasonChunk)
            {
                try
                {
                    season._colorPalette = await MovieDbImageManager.ColorPalette("poster", season.Poster);
                }
                catch (Exception)
                {
                    season._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Season palette job completed, updated: {Count}", seasons.Sum(x => x.Length));

    }
}
