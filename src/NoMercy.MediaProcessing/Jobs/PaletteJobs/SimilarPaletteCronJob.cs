using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class SimilarPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<SimilarPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
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
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} similar chunks to process", similars.Count);

        foreach (Similar[] similarChunk in similars)
        {
            _logger.LogTrace("Processing similar chunk of size: {Size}", similarChunk.Length);

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
