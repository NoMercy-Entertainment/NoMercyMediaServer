using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ShowPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<ShowPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(10);
    public string JobName => "Tv ColorPalette Job";

    public ShowPaletteCronJob(ILogger<ShowPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Tv[]> tvs = _context.Tvs
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToList()
            .Chunk(10)
            .ToList();

        if (tvs.Count == 0) return;

        _logger.LogTrace("Found {Count} tv chunks to process", tvs.Count);

        foreach (Tv[] tvChunk in tvs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Tv tv in tvChunk)
            {
                try
                {
                    tv._colorPalette = await MovieDbImageManager
                        .MultiColorPalette([
                            new("poster", tv.Poster),
                            new("backdrop", tv.Backdrop)
                        ]);
                }
                catch (Exception)
                {
                    tv._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Tv palette job completed, updated: {Count}", tvs.Sum(x => x.Length));

    }
}
