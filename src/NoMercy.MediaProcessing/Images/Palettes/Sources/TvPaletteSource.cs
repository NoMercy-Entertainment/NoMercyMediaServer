using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class TvPaletteSource : IPaletteSource
{
    public string EntityType => "tv";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Tvs.Where(t => t.Id == id)
            .Select(t => t._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Tv? tv = await db.Tvs.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tv is null)
            return PaletteResult.NoImage();
        if (tv.Poster is null && tv.Backdrop is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.MultiColorPalette([
            new("poster", tv.Poster),
            new("backdrop", tv.Backdrop),
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
            .Tvs.Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t._colorPalette, json), ct);
    }
}
