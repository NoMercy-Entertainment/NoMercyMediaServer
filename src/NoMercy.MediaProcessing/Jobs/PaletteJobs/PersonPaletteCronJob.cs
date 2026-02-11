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
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class PersonPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<PersonPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
    public string JobName => "Person ColorPalette Job";

    public PersonPaletteCronJob(ILogger<PersonPaletteCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Person[]> people = _context.People
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} similar chunks to process", people.Count);

        foreach (Person[] peopleChunk in people)
        {
            _logger.LogTrace("Processing similar chunk of size: {Size}", peopleChunk.Length);

            foreach (Person person in peopleChunk)
            {
                try
                {
                    person._colorPalette = await MovieDbImageManager.ColorPalette("profile", person.Profile);
                }
                catch (Exception)
                {
                    person._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogTrace("Person palette job completed, updated: {Count}", people.Sum(x => x.Length));

    }
}
