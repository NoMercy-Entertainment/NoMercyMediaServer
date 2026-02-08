using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class SeasonPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<SeasonPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
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
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} season chunks to process", seasons.Count);

        foreach (Season[] seasonChunk in seasons)
        {
            _logger.LogTrace("Processing season chunk of size: {Size}", seasonChunk.Length);

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
