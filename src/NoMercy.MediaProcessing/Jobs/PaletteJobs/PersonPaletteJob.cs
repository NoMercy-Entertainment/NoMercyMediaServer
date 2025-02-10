// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class PersonPaletteJob : AbstractPaletteJob<Person>
{
    public override string QueueName => "image";
    public override int Priority => 2;

    public override async Task Handle()
    {
        await using MediaContext context = new();

        List<Person> people = context.People
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => Storage
                .Select(y => y.Profile)
                .Contains(x.Profile))
            .ToList();

        foreach (Person? person in people)
            person._colorPalette = await MovieDbImageManager
                .ColorPalette("person", person.Profile);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception)
        {
            // ignored
        }

        Logger.App($"Person palettes updated: {people.Count}", LogEventLevel.Verbose);
    }
}