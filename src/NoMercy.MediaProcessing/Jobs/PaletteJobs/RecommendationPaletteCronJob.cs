using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class RecommendationPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<RecommendationPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
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
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} recommendation chunks to process", recommendations.Count);

        foreach (Recommendation[] recommendationChunk in recommendations)
        {
            _logger.LogTrace("Processing recommendation chunk of size: {Size}", recommendationChunk.Length);

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
