using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class TvPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<TvPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
    public string JobName => "Tv ColorPalette Job";

    public TvPaletteCronJob(ILogger<TvPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Tv[]> tvs = _context.Tvs
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} tv chunks to process", tvs.Count);

        foreach (Tv[] tvChunk in tvs)
        {
            _logger.LogTrace("Processing tv chunk of size: {Size}", tvChunk.Length);

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
