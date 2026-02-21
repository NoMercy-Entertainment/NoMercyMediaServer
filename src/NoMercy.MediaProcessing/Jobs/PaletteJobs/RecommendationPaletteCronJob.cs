using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class RecommendationPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<RecommendationPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(30);
    public string JobName => "Recommendations ColorPalette Job";

    public RecommendationPaletteCronJob(ILogger<RecommendationPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Recommendation[]> recommendations = _context.Recommendations
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.TvFrom != null ? x.TvFrom.UpdatedAt : x.MovieFrom!.UpdatedAt)
            .Take(100)
            .ToList()
            .Chunk(10)
            .ToList();

        if (recommendations.Count == 0) return;

        _logger.LogTrace("Found {Count} recommendation chunks to process", recommendations.Count);

        foreach (Recommendation[] recommendationChunk in recommendations)
        {
            if (cancellationToken.IsCancellationRequested) break;

            await Parallel.ForEachAsync(recommendationChunk, Config.ParallelOptions, async (recommendation, _) =>
            {
                try
                {
                    recommendation._colorPalette = await MovieDbImageManager
                        .MultiColorPalette([
                            new("poster", recommendation.Poster),
                            new("backdrop", recommendation.Backdrop)
                        ]);
                }
                catch (Exception)
                {
                    recommendation._colorPalette = "{}";
                }
            });

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Recommendation palette job completed, updated: {Count}", recommendations.Sum(x => x.Length));
    }
}
