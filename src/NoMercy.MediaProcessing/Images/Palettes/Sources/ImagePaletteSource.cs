using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class ImagePaletteSource : IPaletteSource
{
    public string EntityType => "image";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        return await db
            .Images.Where(i => i.Id == id)
            .Select(i => i._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        int id = int.Parse(entityId);
        Image? image = await db.Images.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (image is null)
            return PaletteResult.NoImage();

        // Guard: only TMDB-hosted images, no SVGs, and matching language
        if (image.Site != "https://image.tmdb.org/t/p/")
            return PaletteResult.NoImage();
        if (image.FilePath.EndsWith(".svg"))
            return PaletteResult.NoImage();

        string lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        bool languageOk =
            image.Iso6391 is null
            || image.Iso6391 == "en"
            || image.Iso6391 == ""
            || image.Iso6391 == lang;
        if (!languageOk)
            return PaletteResult.NoImage();

        string json = await MovieDbImageManager.ColorPalette("image", image.FilePath);
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
            .Images.Where(i => i.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i._colorPalette, json), ct);
    }
}
