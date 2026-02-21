using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.People;
using NoMercy.MediaProcessing.Images;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class PersonPaletteCronJob : ICronJobExecutor
{
    private readonly ILogger<PersonPaletteCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(30);
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
            .Take(100)
            .ToList()
            .Chunk(10)
            .ToList();

        if (people.Count == 0) return;

        _logger.LogTrace("Found {Count} person chunks to process", people.Count);

        foreach (Person[] peopleChunk in people)
        {
            if (cancellationToken.IsCancellationRequested) break;

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
