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

public class ArtistPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<ArtistPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinute();
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
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} artist chunks to process", artists.Count);

        foreach (Artist[] artistChunk in artists)
        {
            _logger.LogTrace("Processing artist chunk of size: {Size}", artistChunk.Length);

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
