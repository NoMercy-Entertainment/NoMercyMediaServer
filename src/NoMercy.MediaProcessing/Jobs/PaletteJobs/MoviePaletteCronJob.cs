using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class MoviePaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<MoviePaletteCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().Daily();
    public string JobName => "Movie ColorPalette Job";

    public MoviePaletteCronJob(ILogger<MoviePaletteCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        await using MediaContext context = new();

        List<Movie[]> movies = context.Movies
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();
        
        _logger.LogTrace("Found {Count} movie chunks to process", movies.Count);

        foreach (Movie[] movieChunk in movies)
        {
            _logger.LogTrace("Processing movie chunk of size: {Size}", movieChunk.Length);

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
                catch (Exception e)
                {
                    movie._colorPalette = "{}";
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            
        }

        _logger.LogTrace("Movie palette job completed, updated: {Count}", movies.Sum(x => x.Length));

    }
}