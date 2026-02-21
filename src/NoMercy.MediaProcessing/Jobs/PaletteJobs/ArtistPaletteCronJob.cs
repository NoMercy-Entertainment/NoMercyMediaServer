using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ArtistPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<ArtistPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryHours(2);
    public string JobName => "Artist ColorPalette Job";

    public ArtistPaletteCronJob(ILogger<ArtistPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Artist[]> artists = _context.Artists
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && x.Cover != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToList()
            .Chunk(5)
            .ToList();

        if (artists.Count == 0) return;

        _logger.LogTrace("Found {Count} artist chunks to process", artists.Count);

        foreach (Artist[] artistChunk in artists)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Artist artist in artistChunk)
            {
                try
                {
                    artist._colorPalette = await MovieDbImageManager
                        .ColorPalette("cover", AppFiles.MusicImagesPath + artist.Cover);
                }
                catch (Exception)
                {
                    artist._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTrace("Artist palette job completed, updated: {Count}", artists.Sum(x => x.Length));

    }
}
