using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class SeasonPaletteSource : IPaletteSource
{
    public string EntityType => "season";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Seasons.Where(s => s.Id == id)
            .Select(s => s._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Season? season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (season is null)
            return PaletteResult.NoImage();
        if (season.Poster is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.ColorPalette("poster", season.Poster);
        return string.IsNullOrWhiteSpace(json)
            ? PaletteResult.NoImage()
            : PaletteResult.Success(json);
    }

    public async Task PersistAsync(
        MediaContext db,
        string entityId,
        string json,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        await db
            .Seasons.Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x._colorPalette, json), ct);
    }
}
