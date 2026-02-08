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
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
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
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} collection chunks to process", collections.Count);

        foreach (Collection[] collectionChunk in collections)
        {
            _logger.LogTrace("Processing collection chunk of size: {Size}", collectionChunk.Length);

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
