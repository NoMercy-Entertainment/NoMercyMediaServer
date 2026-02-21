using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class AlbumPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<AlbumPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryHours(2);
    public string JobName => "Album ColorPalette Job";

    public AlbumPaletteCronJob(ILogger<AlbumPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Album[]> albums = _context.Albums
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && x.Cover != null)
            .Include(x => x.Images)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToList()
            .Chunk(5)
            .ToList();

        if (albums.Count == 0) return;

        _logger.LogTrace("Found {Count} album chunks to process", albums.Count);

        foreach (Album[] albumChunk in albums)
        {
            if (cancellationToken.IsCancellationRequested) break;

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

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTrace("Album palette job completed, updated: {Count}", albums.Sum(x => x.Length));

    }
}
