using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class EpisodePaletteSource : IPaletteSource
{
    public string EntityType => "episode";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Episodes.Where(e => e.Id == id)
            .Select(e => e._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Episode? episode = await db.Episodes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (episode is null)
            return PaletteResult.NoImage();
        if (episode.Still is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.ColorPalette("still", episode.Still);
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
            .Episodes.Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e._colorPalette, json), ct);
    }
}
