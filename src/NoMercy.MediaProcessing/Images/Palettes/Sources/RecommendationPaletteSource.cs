using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class RecommendationPaletteSource : IPaletteSource
{
    public string EntityType => "recommendation";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Recommendations.Where(r => r.Id == id)
            .Select(r => r._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Recommendation? recommendation = await db.Recommendations.FirstOrDefaultAsync(
            r => r.Id == id,
            ct
        );
        if (recommendation is null)
            return PaletteResult.NoImage();
        if (recommendation.Poster is null && recommendation.Backdrop is null)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.MultiColorPalette([
            new("poster", recommendation.Poster),
            new("backdrop", recommendation.Backdrop),
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
            .Recommendations.Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r._colorPalette, json), ct);
    }
}
