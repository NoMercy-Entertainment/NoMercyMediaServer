using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class SimilarPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<SimilarPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(30);
    public string JobName => "Similar ColorPalette Job";

    public SimilarPaletteCronJob(ILogger<SimilarPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Similar[]> similars = _context.Similar
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.TvFrom != null ? x.TvFrom.UpdatedAt : x.MovieFrom!.UpdatedAt)
            .Take(100)
            .ToList()
            .Chunk(10)
            .ToList();

        if (similars.Count == 0) return;

        _logger.LogTrace("Found {Count} similar chunks to process", similars.Count);

        foreach (Similar[] similarChunk in similars)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Similar similar in similarChunk)
            {
                try
                {
                    similar._colorPalette = await MovieDbImageManager
                        .MultiColorPalette([
                            new("poster", similar.Poster),
                            new("backdrop", similar.Backdrop)
                        ]);
                }
                catch (Exception)
                {
                    similar._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Similar palette job completed, updated: {Count}", similars.Sum(x => x.Length));

    }
}
