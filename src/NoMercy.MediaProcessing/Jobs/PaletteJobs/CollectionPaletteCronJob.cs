using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class CollectionPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<CollectionPaletteCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().EveryHour();
    public string JobName => "Collection ColorPalette Job";

    public CollectionPaletteCronJob(ILogger<CollectionPaletteCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        await using MediaContext context = new();

        List<Collection[]> collecttions = context.Collections
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(200)
            .ToList()
            .Chunk(5)
            .ToList();
        
        _logger.LogTrace("Found {Count} collecttion chunks to process", collecttions.Count);

        foreach (Collection[] collecttionChunk in collecttions)
        {
            _logger.LogTrace("Processing collecttion chunk of size: {Size}", collecttionChunk.Length);

            foreach (Collection collecttion in collecttionChunk)
            {
                collecttion._colorPalette = await MovieDbImageManager
                    .MultiColorPalette([
                        new("poster", collecttion.Poster),
                        new("backdrop", collecttion.Backdrop)
                    ]);

                context.Collections.Update(collecttion);
            }
        }
        
        await context.SaveChangesAsync(cancellationToken);
            
        _logger.LogTrace("Collection palette job completed, updated: {Count}", collecttions.Sum(x => x.Length));

    }
}