using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ImagePaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<ImagePaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryHour();
    public string JobName => "Image ColorPalette Job";

    public ImagePaletteCronJob(ILogger<ImagePaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Starting image palette job");

        List<Image[]> images = _context.Images
            .Where(i => i.Site == "https://image.tmdb.org/t/p/")
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && !x.FilePath.EndsWith(".svg"))
            .Where(e => e.Iso6391 == null || e.Iso6391 == "en" || e.Iso6391 == "" ||
                        e.Iso6391 == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} image chunks to process", images.Count);

        foreach (Image[] imageChunk in images)
        {
            _logger.LogTrace("Processing image chunk of size: {Size}", imageChunk.Length);

            await Parallel.ForEachAsync(imageChunk, Config.ParallelOptions, async (image, _) =>
            {
                try
                {
                    image._colorPalette = await MovieDbImageManager.ColorPalette("image", image.FilePath);
                }
                catch (Exception)
                {
                    image._colorPalette = "{}";
                }
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTrace("Image palette job completed, processed {Count} images", images.Sum(x => x.Length));
    }
}
