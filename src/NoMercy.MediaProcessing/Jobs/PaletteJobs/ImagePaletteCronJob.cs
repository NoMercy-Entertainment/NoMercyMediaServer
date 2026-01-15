using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ImagePaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<ImagePaletteCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().EveryHour();
    public string JobName => "Image ColorPalette Job";

    public ImagePaletteCronJob(ILogger<ImagePaletteCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Starting image palette job");
        
        await using MediaContext context = new();

        List<Image[]> images = context.Images
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
                catch (Exception e)
                {
                    image._colorPalette = "{}";
                }
            });
            
            await context.SaveChangesAsync(cancellationToken);
        }
            
        _logger.LogTrace("Image palette job completed, processed {Count} images", images.Sum(x => x.Length));
    }
}