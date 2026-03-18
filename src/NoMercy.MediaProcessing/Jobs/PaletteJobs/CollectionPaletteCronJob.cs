using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class CollectionPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<CollectionPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(30);
    public string JobName => "Collection ColorPalette Job";

    public CollectionPaletteCronJob(ILogger<CollectionPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Collection[]> collections = _context.Collections
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(100)
            .ToList()
            .Chunk(10)
            .ToList();

        if (collections.Count == 0) return;

        _logger.LogTrace("Found {Count} collection chunks to process", collections.Count);

        foreach (Collection[] collectionChunk in collections)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Collection collection in collectionChunk)
            {
                try
                {
                    collection._colorPalette = await MovieDbImageManager
                        .MultiColorPalette([
                            new("poster", collection.Poster),
                            new("backdrop", collection.Backdrop)
                        ]);
                }
                catch (Exception)
                {
                    collection._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Collection palette job completed, updated: {Count}", collections.Sum(x => x.Length));

    }
}
