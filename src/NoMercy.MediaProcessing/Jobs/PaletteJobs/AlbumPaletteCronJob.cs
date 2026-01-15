using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class AlbumPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<AlbumPaletteCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().Daily();
    public string JobName => "Album ColorPalette Job";

    public AlbumPaletteCronJob(ILogger<AlbumPaletteCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        await using MediaContext context = new();

        List<Album[]> albums = context.Albums
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && x.Cover != null)
            .Include(x => x.Images)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();
        
        _logger.LogTrace("Found {Count} album chunks to process", albums.Count);

        foreach (Album[] albumChunk in albums)
        {
            _logger.LogTrace("Processing album chunk of size: {Size}", albumChunk.Length);

            foreach (Album album in albumChunk)
            {
                try
                {
                    album._colorPalette = await CoverArtImageManagerManager
                        .ColorPalette("cover", new(AppFiles.MusicImagesPath + album.Cover));
                }
                catch (Exception)
                {
                    album._colorPalette = "{}";
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTrace("Album palette job completed, updated: {Count}", albums.Sum(x => x.Length));

    }
}