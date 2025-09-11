using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ArtistPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<ArtistPaletteCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().EveryHour();
    public string JobName => "Artist ColorPalette Job";

    public ArtistPaletteCronJob(ILogger<ArtistPaletteCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        await using MediaContext context = new();

        List<Artist[]> artists = context.Artists
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(200)
            .ToList()
            .Chunk(5)
            .ToList();
        
        _logger.LogTrace("Found {Count} artist chunks to process", artists.Count);

        foreach (Artist[] artistChunk in artists)
        {
            _logger.LogTrace("Processing artist chunk of size: {Size}", artistChunk.Length);

            foreach (Artist artist in artistChunk)
            {
                artist._colorPalette = await MovieDbImageManager
                    .ColorPalette("cover", artist.Cover);

                context.Artists.Update(artist);
            }
        }
        
        await context.SaveChangesAsync(cancellationToken);
            
        _logger.LogTrace("Artist palette job completed, updated: {Count}", artists.Sum(x => x.Length));

    }
}