using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class CollectionPaletteSource : IPaletteSource
{
    public string EntityType => "collection";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Collections.Where(c => c.Id == id)
            .Select(c => c._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Collection? collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null)
            return PaletteResult.NoImage();
        if (collection.Poster is null && collection.Backdrop is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.MultiColorPalette([
            new("poster", collection.Poster),
            new("backdrop", collection.Backdrop),
        ]);
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
            .Collections.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c._colorPalette, json), ct);
    }
}
