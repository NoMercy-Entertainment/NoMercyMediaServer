using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class AlbumPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<AlbumPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
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

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTrace("Album palette job completed, updated: {Count}", albums.Sum(x => x.Length));

    }
}
