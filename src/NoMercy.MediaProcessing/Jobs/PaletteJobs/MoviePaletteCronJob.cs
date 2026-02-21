using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class MoviePaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<MoviePaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(10);
    public string JobName => "Movie ColorPalette Job";

    public MoviePaletteCronJob(ILogger<MoviePaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Movie[]> movies = _context.Movies
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToList()
            .Chunk(10)
            .ToList();

        if (movies.Count == 0) return;

        _logger.LogTrace("Found {Count} movie chunks to process", movies.Count);

        foreach (Movie[] movieChunk in movies)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (Movie movie in movieChunk)
            {
                try
                {
                    movie._colorPalette = await MovieDbImageManager
                        .MultiColorPalette([
                            new("poster", movie.Poster),
                            new("backdrop", movie.Backdrop)
                        ]);
                }
                catch (Exception)
                {
                    movie._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Movie palette job completed, updated: {Count}", movies.Sum(x => x.Length));

    }
}
